using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WinPartFlash.Gui.MacOS;

/// <summary>
/// Per-operation elevation via `osascript -e 'do shell script ... with
/// administrator privileges'`.  Used when SMAppService LaunchDaemon
/// registration is unavailable (unsigned dev / `dotnet run` builds).
///
/// One macOS authorization prompt per Save / Flash operation; the helper
/// runs for the entire duration of the stream as root and exits when the
/// GUI closes the stream.  GUI-side communication is a per-op AF_UNIX socket
/// in /tmp protected by a 32-byte random token sent by the helper as its
/// first bytes after connect.
/// </summary>
[SupportedOSPlatform("MacOS")]
public sealed partial class OsascriptPrivilegedDiskGateway : IPrivilegedDiskGateway
{
    private const int TokenBytes = 32;
    private const int TokenHexLen = TokenBytes * 2;

    private readonly string _helperPath;
    private readonly ILogger<OsascriptPrivilegedDiskGateway> _logger;

    public OsascriptPrivilegedDiskGateway(ILogger<OsascriptPrivilegedDiskGateway> logger)
    {
        _logger = logger;
        _helperPath = Path.Combine(AppContext.BaseDirectory, "com.hcgstudio.winpartflash.helper");
        LogConstructed(_helperPath);
    }

    public Task UnmountDiskAsync(string device, CancellationToken cancellationToken)
        => MacOSDiskUtil.UnmountDiskAsync(device, cancellationToken);

    public async Task<Stream> OpenPartitionAsync(
        string device,
        ulong offset,
        ulong length,
        FileAccess access,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_helperPath))
        {
            LogHelperMissing(_helperPath);
            throw new PrivilegedHelperUnavailableException(
                $"Privileged helper not found at {_helperPath}.");
        }

        ValidateDevice(device);

        var op = access.HasFlag(FileAccess.Write) ? "write" : "read";
        LogElevationRequested(op, device, offset, length);

        var token = GenerateTokenHex();
        var socketPath = $"/tmp/wpf-{Guid.NewGuid():N}".Substring(0, 20) + ".sock";

        // Best-effort cleanup if a stale socket exists.
        try { File.Delete(socketPath); } catch { /* ignore */ }

        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            // 0600 — only this user (and root) may even see the socket; the
            // token defends against the (already narrow) race.
            Chmod(socketPath, 0b110_000_000);
            listener.Listen(1);

            var process = StartOsascript(device, op, offset, length, socketPath, token);
            LogOsascriptStarted(process.Id, device);

            Socket connection;
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var acceptTask = listener.AcceptAsync(linkedCts.Token).AsTask();
                var processTask = process.WaitForExitAsync(linkedCts.Token);
                var done = await Task.WhenAny(acceptTask, processTask);

                if (done == processTask)
                {
                    var stderr = await process.StandardError.ReadToEndAsync(CancellationToken.None);
                    if (stderr.Contains("-128") || stderr.Contains("User canceled", StringComparison.OrdinalIgnoreCase))
                    {
                        LogAuthorizationCancelled();
                        throw new PrivilegedAuthorizationCancelledException(stderr.Trim());
                    }
                    LogAuthorizationFailed(process.ExitCode, stderr.Trim());
                    throw new PrivilegedAuthorizationFailedException(
                        string.IsNullOrWhiteSpace(stderr) ? $"osascript exited {process.ExitCode}" : stderr.Trim());
                }

                connection = await acceptTask;
                LogHelperConnected();
            }
            catch
            {
                TryKill(process);
                throw;
            }

            var verifyBuf = new byte[TokenHexLen];
            var read = 0;
            while (read < TokenHexLen)
            {
                var n = await connection.ReceiveAsync(verifyBuf.AsMemory(read), cancellationToken);
                if (n == 0)
                {
                    connection.Dispose();
                    TryKill(process);
                    LogHelperClosedEarly();
                    throw new PrivilegedHelperUnavailableException("Helper closed the connection before sending an auth token.");
                }
                read += n;
            }
            if (!CryptographicOperations.FixedTimeEquals(verifyBuf, Encoding.ASCII.GetBytes(token)))
            {
                connection.Dispose();
                TryKill(process);
                LogTokenMismatch();
                throw new PrivilegedHelperUnavailableException("Helper auth token mismatch.");
            }

            LogStreamReady(op, device);
            return new HelperBackedStream(
                connection,
                listener,
                process,
                socketPath,
                access,
                _logger);
        }
        catch
        {
            listener.Dispose();
            try { File.Delete(socketPath); } catch { /* ignore */ }
            throw;
        }
    }

    private Process StartOsascript(
        string device, string op, ulong offset, ulong length, string socketPath, string token)
    {
        // All inputs are validated/whitelisted before we get here, so building
        // the AppleScript by interpolation is safe.  Quote with single quotes
        // inside the shell command so AppleScript's own double-quote escape
        // surface stays small (AppleScript string only needs \" and \\).
        var shell =
            $"\"{EscapeForAppleScript(_helperPath)}\"" +
            $" --device {device}" +
            $" --op {op}" +
            $" --offset {offset.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $" --length {length.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $" --socket {EscapeForShell(socketPath)}" +
            $" --token {token}";

        var script = $"do shell script \"{EscapeForAppleScript(shell)}\" with administrator privileges";

        var psi = new ProcessStartInfo("/usr/bin/osascript")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add(script);

        return Process.Start(psi)
              ?? throw new PrivilegedHelperUnavailableException("Failed to spawn osascript.");
    }

    private static string EscapeForAppleScript(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeForShell(string s)
        // socketPath only ever contains [a-z0-9-./], chosen by us — but be defensive.
        => "'" + s.Replace("'", "'\\''") + "'";

    private static void ValidateDevice(string device)
    {
        // Mirror the helper's whitelist so we fail fast rather than handing a
        // bad arg to the privileged side.
        if (!System.Text.RegularExpressions.Regex.IsMatch(device, @"^/dev/r?disk\d+$"))
            throw new ArgumentException($"Refusing to operate on non-whitelisted device {device}.", nameof(device));
    }

    private static string GenerateTokenHex()
    {
        Span<byte> bytes = stackalloc byte[TokenBytes];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(TokenHexLen);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* ignore */ }
    }

    [LoggerMessage(EventId = 200, Level = LogLevel.Debug,
        Message = "OsascriptPrivilegedDiskGateway initialised; helper at {HelperPath}.")]
    private partial void LogConstructed(string helperPath);

    [LoggerMessage(EventId = 201, Level = LogLevel.Error,
        Message = "Privileged helper binary missing at {HelperPath}.")]
    private partial void LogHelperMissing(string helperPath);

    [LoggerMessage(EventId = 202, Level = LogLevel.Information,
        Message = "Requesting macOS elevation: op={Op} device={Device} offset={Offset} length={Length}")]
    private partial void LogElevationRequested(string op, string device, ulong offset, ulong length);

    [LoggerMessage(EventId = 203, Level = LogLevel.Debug,
        Message = "osascript started (pid={Pid}) for {Device}; awaiting helper connect.")]
    private partial void LogOsascriptStarted(int pid, string device);

    [LoggerMessage(EventId = 204, Level = LogLevel.Information,
        Message = "Helper connected on socket; verifying auth token.")]
    private partial void LogHelperConnected();

    [LoggerMessage(EventId = 205, Level = LogLevel.Warning,
        Message = "User cancelled the macOS authorization prompt.")]
    private partial void LogAuthorizationCancelled();

    [LoggerMessage(EventId = 206, Level = LogLevel.Error,
        Message = "Authorization failed (osascript exit={ExitCode}): {Stderr}")]
    private partial void LogAuthorizationFailed(int exitCode, string stderr);

    [LoggerMessage(EventId = 207, Level = LogLevel.Error,
        Message = "Helper closed the connection before sending an auth token.")]
    private partial void LogHelperClosedEarly();

    [LoggerMessage(EventId = 208, Level = LogLevel.Error,
        Message = "Helper auth token mismatch — refusing the connection.")]
    private partial void LogTokenMismatch();

    [LoggerMessage(EventId = 209, Level = LogLevel.Information,
        Message = "Privileged stream ready: op={Op} device={Device}")]
    private partial void LogStreamReady(string op, string device);

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, uint mode);

    /// <summary>
    /// Stream that wraps the AF_UNIX connection to the helper and owns the
    /// helper process + listener + socket file lifecycle.
    /// </summary>
    private sealed partial class HelperBackedStream : Stream
    {
        private readonly NetworkStream _inner;
        private readonly Socket _connection;
        private readonly Socket _listener;
        private readonly Process _helper;
        private readonly string _socketPath;
        private readonly FileAccess _access;
        private bool _disposed;

        public HelperBackedStream(
            Socket connection, Socket listener, Process helper,
            string socketPath, FileAccess access,
            ILogger logger)
        {
            _connection = connection;
            _listener = listener;
            _helper = helper;
            _socketPath = socketPath;
            _access = access;
            _logger = logger;
            _inner = new NetworkStream(connection, ownsSocket: false);
        }

        private readonly ILogger _logger;

        public override bool CanRead => _access.HasFlag(FileAccess.Read);
        public override bool CanWrite => _access.HasFlag(FileAccess.Write);
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken ct) => _inner.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => _inner.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            => _inner.ReadAsync(buffer, ct);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => _inner.WriteAsync(buffer, offset, count, ct);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
            => _inner.WriteAsync(buffer, ct);
        public override long Seek(long o, SeekOrigin or) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (CanWrite)
                    try { _connection.Shutdown(SocketShutdown.Send); } catch { /* ignore */ }

                _inner.Dispose();
                _connection.Dispose();
                _listener.Dispose();

                // Give the helper up to a few seconds to finish flushing+exiting cleanly.
                try
                {
                    if (!_helper.WaitForExit(TimeSpan.FromSeconds(5)))
                        _helper.Kill(entireProcessTree: true);
                }
                catch { /* ignore */ }
                _helper.Dispose();

                try { File.Delete(_socketPath); } catch { /* ignore */ }
                LogStreamDisposed();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        [LoggerMessage(EventId = 220, Level = LogLevel.Information,
            Message = "Privileged stream disposed; helper exited.")]
        private partial void LogStreamDisposed();
    }
}

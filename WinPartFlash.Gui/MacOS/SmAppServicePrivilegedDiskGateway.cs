using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WinPartFlash.Gui.MacOS.Interop;

namespace WinPartFlash.Gui.MacOS;

/// <summary>
/// Production gateway: registers the bundled privileged helper as a
/// LaunchDaemon via <c>SMAppService</c> on first run, then triggers it over
/// XPC for each partition open.  The bulk data channel is the same AF_UNIX
/// socket + 32-byte token used by <see cref="OsascriptPrivilegedDiskGateway"/>;
/// XPC only carries the connect-back parameters, so
/// <see cref="HelperBackedStream"/> is identical across both paths.
///
/// Requires a signed bundle whose code requirement matches the plist's
/// <c>SMAuthorizedClients</c> entry.  On unsigned/dev builds
/// <see cref="TryRegisterAsync"/> returns false and the factory falls back
/// to <see cref="OsascriptPrivilegedDiskGateway"/>.
/// </summary>
[SupportedOSPlatform("MacOS")]
public sealed partial class SmAppServicePrivilegedDiskGateway : IPrivilegedDiskGateway
{
    public const string DaemonPlistName = "com.hcgstudio.winpartflash.helper.plist";
    public const string MachServiceName = "com.hcgstudio.winpartflash.helper";

    private const int TokenBytes = 32;
    private const int TokenHexLen = TokenBytes * 2;

    private readonly ILogger<SmAppServicePrivilegedDiskGateway> _logger;

    public SmAppServicePrivilegedDiskGateway(ILogger<SmAppServicePrivilegedDiskGateway> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attempts to register the daemon via SMAppService.  Returns true only
    /// when registration succeeds and the helper is ready to accept XPC
    /// messages.  "Not approved" is treated as a soft failure so the factory
    /// can fall back to osascript while the user toggles the background
    /// item on.
    /// </summary>
    public static Task<bool> TryRegisterAsync(
        ILogger<SmAppServicePrivilegedDiskGateway> logger,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        SMAppServiceInterop.RegisterResult result;
        try
        {
            result = SMAppServiceInterop.Register(DaemonPlistName);
        }
        catch (Exception ex)
        {
            LogRegistrationThrew(logger, ex.Message);
            return Task.FromResult(false);
        }

        switch (result.Status)
        {
            case SMAppServiceInterop.RegisterStatus.Ok:
                LogRegistrationOk(logger, DaemonPlistName);
                return Task.FromResult(true);
            case SMAppServiceInterop.RegisterStatus.Unsigned:
                LogRegistrationUnsigned(logger);
                return Task.FromResult(false);
            case SMAppServiceInterop.RegisterStatus.NotApproved:
                LogRegistrationNotApproved(logger);
                return Task.FromResult(false);
            case SMAppServiceInterop.RegisterStatus.FrameworkUnavailable:
                LogRegistrationFrameworkMissing(logger);
                return Task.FromResult(false);
            default:
                LogRegistrationFailed(logger,
                    result.Domain ?? "(unknown)", result.Code,
                    result.LocalizedDescription ?? "(no description)");
                return Task.FromResult(false);
        }
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
        ValidateDevice(device);

        var op = access.HasFlag(FileAccess.Write) ? "write" : "read";
        LogTriggerRequested(op, device, offset, length);

        var token = GenerateTokenHex();
        var socketPath = $"/tmp/wpf-{Guid.NewGuid():N}".Substring(0, 20) + ".sock";

        try { File.Delete(socketPath); } catch { /* ignore */ }

        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            Chmod(socketPath, 0b110_000_000); // 0600
            listener.Listen(1);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var acceptTask = listener.AcceptAsync(linkedCts.Token).AsTask();

            var triggerTask = Task.Run(() =>
            {
                var errBuf = new byte[512];
                var rc = NativeSendTrigger(
                    MachServiceName, device, op, offset, length,
                    socketPath, token,
                    out var statusCode,
                    errBuf, (UIntPtr)errBuf.Length);
                var err = DecodeNativeString(errBuf);
                return (rc, statusCode, err);
            }, linkedCts.Token);

            var done = await Task.WhenAny(acceptTask, triggerTask);
            if (done == triggerTask)
            {
                var (rc, statusCode, err) = await triggerTask;
                LogTriggerCompleted(rc, statusCode, err ?? "");
                if (rc != 0 || statusCode != 0)
                {
                    throw new PrivilegedHelperUnavailableException(
                        $"Helper trigger failed (rc={rc}, status={statusCode}): {err}");
                }
                // Helper replied ok but closed the connection before the
                // socket acceptance — shouldn't happen, but surface it.
                throw new PrivilegedHelperUnavailableException(
                    "Helper acknowledged the trigger but did not connect back.");
            }

            var connection = await acceptTask;
            LogHelperConnected();

            var verifyBuf = new byte[TokenHexLen];
            var read = 0;
            while (read < TokenHexLen)
            {
                var n = await connection.ReceiveAsync(verifyBuf.AsMemory(read), cancellationToken);
                if (n == 0)
                {
                    connection.Dispose();
                    LogHelperClosedEarly();
                    throw new PrivilegedHelperUnavailableException(
                        "Helper closed the connection before sending an auth token.");
                }
                read += n;
            }
            if (!CryptographicOperations.FixedTimeEquals(verifyBuf, Encoding.ASCII.GetBytes(token)))
            {
                connection.Dispose();
                LogTokenMismatch();
                throw new PrivilegedHelperUnavailableException("Helper auth token mismatch.");
            }

            LogStreamReady(op, device);
            return new HelperBackedStream(
                connection, listener, socketPath, access, _logger,
                onDispose: null);
        }
        catch
        {
            listener.Dispose();
            try { File.Delete(socketPath); } catch { /* ignore */ }
            throw;
        }
    }

    private static void ValidateDevice(string device)
    {
        if (!Regex.IsMatch(device, @"^/dev/r?disk\d+$"))
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

    private static string? DecodeNativeString(byte[] buf)
    {
        var len = Array.IndexOf(buf, (byte)0);
        if (len <= 0) return null;
        return Encoding.UTF8.GetString(buf, 0, len);
    }

    [DllImport("WinPartFlashLib", EntryPoint = "wpf_xpc_send_trigger")]
    private static extern int NativeSendTrigger(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string serviceName,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string device,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string op,
        ulong offset,
        ulong length,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string socketPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string token,
        out long outStatusCode,
        [Out] byte[] errBuf,
        UIntPtr errBufLen);

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, uint mode);

    [LoggerMessage(EventId = 300, Level = LogLevel.Information,
        Message = "SMAppService registered {PlistName}.")]
    private static partial void LogRegistrationOk(ILogger logger, string plistName);

    [LoggerMessage(EventId = 301, Level = LogLevel.Debug,
        Message = "SMAppService registration skipped: build is unsigned or code requirement not met.")]
    private static partial void LogRegistrationUnsigned(ILogger logger);

    [LoggerMessage(EventId = 302, Level = LogLevel.Warning,
        Message = "SMAppService registration pending: the user must allow the background item in System Settings.")]
    private static partial void LogRegistrationNotApproved(ILogger logger);

    [LoggerMessage(EventId = 303, Level = LogLevel.Debug,
        Message = "SMAppService framework not available; pre-macOS 13 host.")]
    private static partial void LogRegistrationFrameworkMissing(ILogger logger);

    [LoggerMessage(EventId = 304, Level = LogLevel.Error,
        Message = "SMAppService registration failed: domain={Domain} code={Code}: {Description}")]
    private static partial void LogRegistrationFailed(ILogger logger, string domain, long code, string description);

    [LoggerMessage(EventId = 305, Level = LogLevel.Error,
        Message = "SMAppService registration threw: {Message}")]
    private static partial void LogRegistrationThrew(ILogger logger, string message);

    [LoggerMessage(EventId = 310, Level = LogLevel.Information,
        Message = "Triggering helper via XPC: op={Op} device={Device} offset={Offset} length={Length}")]
    private partial void LogTriggerRequested(string op, string device, ulong offset, ulong length);

    [LoggerMessage(EventId = 311, Level = LogLevel.Error,
        Message = "Helper trigger completed with rc={Rc} statusCode={StatusCode} err={Err}")]
    private partial void LogTriggerCompleted(int rc, long statusCode, string err);

    [LoggerMessage(EventId = 312, Level = LogLevel.Information,
        Message = "Helper connected on socket; verifying auth token.")]
    private partial void LogHelperConnected();

    [LoggerMessage(EventId = 313, Level = LogLevel.Error,
        Message = "Helper closed the connection before sending an auth token.")]
    private partial void LogHelperClosedEarly();

    [LoggerMessage(EventId = 314, Level = LogLevel.Error,
        Message = "Helper auth token mismatch — refusing the connection.")]
    private partial void LogTokenMismatch();

    [LoggerMessage(EventId = 315, Level = LogLevel.Information,
        Message = "Privileged stream ready: op={Op} device={Device}")]
    private partial void LogStreamReady(string op, string device);
}

using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WinPartFlash.Gui.MacOS;

/// <summary>
/// Stream that wraps the AF_UNIX connection to the privileged helper and
/// owns the listener, socket file, and an optional cleanup hook for the
/// helper process.  Shared by <see cref="OsascriptPrivilegedDiskGateway"/>
/// (cleanup = kill child process) and
/// <see cref="SmAppServicePrivilegedDiskGateway"/> (cleanup = no-op; the
/// launchd-managed helper exits on its own once the socket closes).
/// </summary>
[SupportedOSPlatform("MacOS")]
internal sealed partial class HelperBackedStream : Stream
{
    private readonly NetworkStream _inner;
    private readonly Socket _connection;
    private readonly Socket _listener;
    private readonly string _socketPath;
    private readonly FileAccess _access;
    private readonly Action? _onDispose;
    private readonly ILogger _logger;
    private bool _disposed;

    public HelperBackedStream(
        Socket connection,
        Socket listener,
        string socketPath,
        FileAccess access,
        ILogger logger,
        Action? onDispose = null)
    {
        _connection = connection;
        _listener = listener;
        _socketPath = socketPath;
        _access = access;
        _logger = logger;
        _onDispose = onDispose;
        _inner = new NetworkStream(connection, ownsSocket: false);
    }

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

            try { _onDispose?.Invoke(); } catch { /* ignore */ }

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

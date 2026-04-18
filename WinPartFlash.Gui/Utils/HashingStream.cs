using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.Utils;

/// <summary>
/// Pass-through Stream wrapper that feeds every byte read or written into
/// an <see cref="IncrementalHash"/>. Used to compute a SHA-256 digest of
/// the raw (uncompressed) side of a copy without a second pass.
/// </summary>
public sealed class HashingStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash;
    private readonly bool _leaveOpen;
    private long _bytesProcessed;

    public HashingStream(Stream inner, HashAlgorithmName algorithm, bool leaveOpen = false)
    {
        _inner = inner;
        _hash = IncrementalHash.CreateHash(algorithm);
        _leaveOpen = leaveOpen;
    }

    public long BytesProcessed => _bytesProcessed;

    public byte[] GetHashAndReset()
    {
        return _hash.GetHashAndReset();
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        if (read > 0)
        {
            _hash.AppendData(buffer, offset, read);
            _bytesProcessed += read;
        }
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        if (read > 0)
        {
            _hash.AppendData(buffer.Span[..read]);
            _bytesProcessed += read;
        }
        return read;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _hash.AppendData(buffer, offset, count);
        _inner.Write(buffer, offset, count);
        _bytesProcessed += count;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _hash.AppendData(buffer.Span);
        await _inner.WriteAsync(buffer, cancellationToken);
        _bytesProcessed += buffer.Length;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
            if (!_leaveOpen) _inner.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        _hash.Dispose();
        if (!_leaveOpen) await _inner.DisposeAsync();
        await base.DisposeAsync();
    }
}

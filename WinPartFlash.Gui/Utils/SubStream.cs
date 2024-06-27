// Modified based on https://github.com/connorhaigh/SubstreamSharp/blob/master/SubstreamSharp/Substream.cs
// Originally Licensed by MIT License Copyright 2021 Connor Haigh
// See https://github.com/connorhaigh/SubstreamSharp/blob/master/Licence.txt

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.Utils;

/// <summary>
///     Represents a sub-stream of an underlying <see cref="Stream" />.
/// </summary>
public class SubStream : Stream
{
    private readonly long _length;

    private readonly long _offset;

    private readonly Stream _stream;

    private long _position;

    /// <summary>
    ///     Creates a new sub-stream instance using the specified underlying stream at the specified offset with the specified
    ///     length.
    /// </summary>
    /// <param name="stream">The underlying stream.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    public SubStream(Stream stream, long offset, long length)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        // Streams must support seeking for the concept of substreams to work.
        // At a pinch in the future we may support a poor man's seek (forward) by reading until the position is correct.

        if (!stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");

        _stream = stream;

        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be less than zero.");

        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be less than zero.");

        _offset = offset;
        _length = length;

        // Seek to position and avoid seeking when read or write
        stream.Seek(offset, SeekOrigin.Begin);
    }

    /// <inheritdoc />
    public override long Length => _length;

    /// <inheritdoc />
    public override bool CanRead => _stream.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => _stream.CanWrite;

    /// <inheritdoc />
    public override bool CanTimeout => _stream.CanTimeout;

    /// <inheritdoc />
    public override int ReadTimeout
    {
        get => _stream.ReadTimeout;
        set => _stream.ReadTimeout = value;
    }

    /// <inheritdoc />
    public override int WriteTimeout
    {
        get => _stream.WriteTimeout;
        set => _stream.WriteTimeout = value;
    }

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException("Position cannot be less than zero.");

            if (value > _length) throw new ArgumentOutOfRangeException("Position cannot be greater than the length.");

            _stream.Position = _offset + (_position = value);
        }
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        if (!_stream.CanRead) throw new NotSupportedException("Underlying stream does not support reading.");

        var actualCount = _stream.Read(buffer[..Convert.ToInt32(Math.Min(buffer.Length, _length - _position))]);

        _position += actualCount;

        return actualCount;
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_stream.CanRead) throw new NotSupportedException("Underlying stream does not support reading.");

        var actualCount = await _stream.ReadAsync(buffer, cancellationToken);

        _position += actualCount;

        return actualCount;
    }

    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (!_stream.CanRead) throw new NotSupportedException("Underlying stream does not support reading.");

        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero.");

        // Limit the count to the remaining length of the stream
        count = Convert.ToInt32(Math.Min(count, _length - _position));

        var actualCount = await _stream.ReadAsync(buffer, offset, count, cancellationToken);

        _position += actualCount;

        return actualCount;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!_stream.CanRead) throw new NotSupportedException("Underlying stream does not support reading.");

        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero.");

        // Limit the count to the remaining length of the stream
        count = Convert.ToInt32(Math.Min(count, _length - _position));
        var actualCount = _stream.Read(buffer, offset, count);

        _position += actualCount;

        return actualCount;
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new())
    {
        if (!_stream.CanWrite)
            throw new NotSupportedException("Underlying stream does not support writing.");

        var count = buffer.Length;

        // Limit the count to the remaining length of the stream
        count = Convert.ToInt32(Math.Min(count, _length - _position));

        await _stream.WriteAsync(buffer[..count], cancellationToken);

        _position += count;
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (!_stream.CanWrite) throw new NotSupportedException("Underlying stream does not support writing.");

        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero.");

        // Limit the count to the remaining length of the stream
        count = Convert.ToInt32(Math.Min(count, _length - _position));

        await _stream.WriteAsync(buffer, offset, count, cancellationToken);

        _position += count;
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (!_stream.CanWrite) throw new NotSupportedException("Underlying stream does not support writing.");

        // Limit the count to the remaining length of the stream
        var count = Convert.ToInt32(Math.Min(buffer.Length, _length - _position));

        _stream.Write(buffer[..count]);
        _position += count;
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!_stream.CanWrite) throw new NotSupportedException("Underlying stream does not support writing.");

        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero.");

        // Limit the count to the remaining length of the stream
        count = Convert.ToInt32(Math.Min(count, _length - _position));
        _stream.Write(buffer, offset, count);

        _position += count;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                if (offset < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset),
                        "Offset cannot be less than zero when seeking from the beginning.");

                if (offset > _length)
                    throw new ArgumentOutOfRangeException(nameof(offset),
                        "Offset cannot be greater than the length of the substream.");

                _stream.Seek(_offset + (_position = offset), SeekOrigin.Begin);

                break;
            case SeekOrigin.End:
                if (offset > 0)
                    throw new ArgumentOutOfRangeException(nameof(offset),
                        "Offset cannot be greater than zero when seeking from the end.");

                if (offset < -_length)
                    throw new ArgumentOutOfRangeException(nameof(offset),
                        "Offset cannot be less than the length of the substream.");

                _stream.Seek(_position = _length + offset, SeekOrigin.End);

                break;
            case SeekOrigin.Current:
                if (_position + offset < 0)
                    throw new NotSupportedException("Attempted to seek before the start of the substream.");

                if (_position + offset > _length)
                    throw new NotSupportedException("Attempted to seek beyond the end of the substream.");

                _stream.Seek(_position += offset, SeekOrigin.Current);

                break;
        }

        return _position;
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        // While other Stream implementations allow the caller to set the length, this does not make much sense in the context of a substream.
        // Perhaps, in the future, we can allow callers to reduce the length, but not expand the length.

        throw new NotSupportedException("Cannot set the length of a fixed sub-stream.");
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _stream.FlushAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override void Flush()
    {
        _stream.Flush();
    }

    /// <inheritdoc />
    public override void Close()
    {
        _stream.Close();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _stream.Dispose();
    }
}
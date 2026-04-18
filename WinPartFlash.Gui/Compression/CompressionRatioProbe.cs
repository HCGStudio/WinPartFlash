using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.Compression;

/// <summary>
/// Runs a streaming probe that compresses a bounded prefix of the source and
/// reports how many bytes ended up in the compressed output.
/// </summary>
public static class CompressionRatioProbe
{
    public readonly record struct Result(long BytesIn, long BytesOut, TimeSpan Elapsed)
    {
        public double Ratio => BytesIn == 0 ? 1.0 : (double)BytesOut / BytesIn;
    }

    public static async Task<Result> RunAsync(
        ICompressionStreamCopier copier,
        Stream source,
        CompressionOptions options,
        long sampleBytes,
        CancellationToken cancellationToken = default)
    {
        using var boundedSource = new LimitingReadStream(source, sampleBytes);
        await using var counter = new CountingWriteStream();
        var sw = Stopwatch.StartNew();
        await copier.CopyToStreamAsync(boundedSource, counter, options, cancellationToken: cancellationToken);
        sw.Stop();
        return new(boundedSource.BytesRead, counter.BytesWritten, sw.Elapsed);
    }

    private sealed class LimitingReadStream(Stream inner, long max) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => max;
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = max - BytesRead;
            if (remaining <= 0) return 0;
            var take = (int)Math.Min(count, remaining);
            var n = inner.Read(buffer, offset, take);
            BytesRead += n;
            return n;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var remaining = max - BytesRead;
            if (remaining <= 0) return 0;
            var slice = buffer.Length > remaining ? buffer[..(int)remaining] : buffer;
            var n = await inner.ReadAsync(slice, cancellationToken);
            BytesRead += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CountingWriteStream : Stream
    {
        public long BytesWritten { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;
        public override long Position { get => BytesWritten; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => BytesWritten += count;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            BytesWritten += buffer.Length;
            return ValueTask.CompletedTask;
        }
    }
}

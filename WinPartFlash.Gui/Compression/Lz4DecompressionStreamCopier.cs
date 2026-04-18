using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Streams;
using WinPartFlash.Gui.Utils;

namespace WinPartFlash.Gui.Compression;

public class Lz4DecompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(
        Stream sourceStream,
        Stream outputStream,
        CompressionOptions options = default,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var decompressionStream = LZ4Stream.Decode(sourceStream);
        await StreamCopyHelper.CopyAsync(decompressionStream, outputStream, progress, cancellationToken);
    }
}

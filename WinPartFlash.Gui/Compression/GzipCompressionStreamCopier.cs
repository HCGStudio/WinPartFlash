using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using WinPartFlash.Gui.Utils;

namespace WinPartFlash.Gui.Compression;

public class GzipCompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(
        Stream sourceStream,
        Stream outputStream,
        CompressionOptions options = default,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var level = MapLevel(options.Level);
        await using var compressionStream = new GZipStream(outputStream, level);
        await StreamCopyHelper.CopyAsync(sourceStream, compressionStream, progress, cancellationToken);
    }

    private static CompressionLevel MapLevel(int? level) => level switch
    {
        1 => CompressionLevel.Fastest,
        2 => CompressionLevel.Optimal,
        3 => CompressionLevel.SmallestSize,
        _ => CompressionLevel.Optimal
    };
}

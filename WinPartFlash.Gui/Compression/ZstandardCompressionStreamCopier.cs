using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinPartFlash.Gui.Utils;
using ZstdSharp;
using ZstdSharp.Unsafe;

namespace WinPartFlash.Gui.Compression;

public class ZstandardCompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(
        Stream sourceStream,
        Stream outputStream,
        CompressionOptions options = default,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var level = options.Level ?? 3;
        await using var compressionStream = new CompressionStream(outputStream, level);
        if (options.Workers is { } workers and > 0)
            compressionStream.SetParameter(ZSTD_cParameter.ZSTD_c_nbWorkers, workers);
        await StreamCopyHelper.CopyAsync(sourceStream, compressionStream, progress, cancellationToken);
    }
}

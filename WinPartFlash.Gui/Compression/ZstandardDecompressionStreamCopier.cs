using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinPartFlash.Gui.Utils;
using ZstdSharp;

namespace WinPartFlash.Gui.Compression;

public class ZstandardDecompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(
        Stream sourceStream,
        Stream outputStream,
        CompressionOptions options = default,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var decompressionStream = new DecompressionStream(sourceStream);
        await StreamCopyHelper.CopyAsync(decompressionStream, outputStream, progress, cancellationToken);
    }
}

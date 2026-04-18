using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Xz;
using WinPartFlash.Gui.Utils;

namespace WinPartFlash.Gui.Compression;

public class XzDecompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(
        Stream sourceStream,
        Stream outputStream,
        CompressionOptions options = default,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var decompressionStream = new XZStream(sourceStream);
        await StreamCopyHelper.CopyAsync(decompressionStream, outputStream, progress, cancellationToken);
    }
}

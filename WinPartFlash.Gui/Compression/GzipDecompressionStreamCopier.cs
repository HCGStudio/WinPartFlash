using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using WinPartFlash.Gui.Utils;

namespace WinPartFlash.Gui.Compression;

public class GzipDecompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(
        Stream sourceStream,
        Stream outputStream,
        CompressionOptions options = default,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var decompressionStream = new GZipStream(sourceStream, CompressionMode.Decompress);
        await StreamCopyHelper.CopyAsync(decompressionStream, outputStream, progress, cancellationToken);
    }
}

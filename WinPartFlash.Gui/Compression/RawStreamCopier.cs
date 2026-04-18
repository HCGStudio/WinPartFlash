using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinPartFlash.Gui.Utils;

namespace WinPartFlash.Gui.Compression;

public class RawStreamCopier : ICompressionStreamCopier
{
    public ValueTask CopyToStreamAsync(
        Stream sourceStream,
        Stream outputStream,
        CompressionOptions options = default,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return StreamCopyHelper.CopyAsync(sourceStream, outputStream, progress, cancellationToken);
    }
}

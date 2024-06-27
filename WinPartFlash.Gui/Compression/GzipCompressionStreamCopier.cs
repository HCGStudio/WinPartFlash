using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.Compression;

public class GzipCompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(Stream sourceStream, Stream outputStream)
    {
        await using var compressionStream = new GZipStream(outputStream, CompressionMode.Compress);
        await sourceStream.CopyToAsync(compressionStream);
    }
}
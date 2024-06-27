using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.Compression;

public class GzipDecompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(Stream sourceStream, Stream outputStream)
    {
        await using var decompressionStream = new GZipStream(sourceStream, CompressionMode.Decompress);
        await decompressionStream.CopyToAsync(outputStream);
    }
}
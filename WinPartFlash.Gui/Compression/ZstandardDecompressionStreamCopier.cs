using System.IO;
using System.Threading.Tasks;
using ZstdSharp;

namespace WinPartFlash.Gui.Compression;

public class ZstandardDecompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(Stream sourceStream, Stream outputStream)
    {
        await using var decompressionStream = new DecompressionStream(sourceStream);
        await decompressionStream.CopyToAsync(outputStream);
    }
}
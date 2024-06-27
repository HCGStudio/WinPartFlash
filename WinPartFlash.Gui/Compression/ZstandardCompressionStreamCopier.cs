using System.IO;
using System.Threading.Tasks;
using ZstdSharp;

namespace WinPartFlash.Gui.Compression;

public class ZstandardCompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(Stream sourceStream, Stream outputStream)
    {
        await using var compressionStream = new CompressionStream(outputStream);
        await sourceStream.CopyToAsync(compressionStream);
    }
}
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Compressors.Xz;

namespace WinPartFlash.Gui.Compression;

public class XzDecompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(Stream sourceStream, Stream outputStream)
    {
        await using var decompressionStream = new XZStream(sourceStream);
        await decompressionStream.CopyToAsync(outputStream);
    }
}
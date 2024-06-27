using System.IO;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.Compression;

public class RawStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(Stream sourceStream, Stream outputStream)
    {
        await sourceStream.CopyToAsync(outputStream);
    }
}
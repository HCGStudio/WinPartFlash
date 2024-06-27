using System.IO;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Streams;

namespace WinPartFlash.Gui.Compression;

public class Lz4DecompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(Stream sourceStream, Stream outputStream)
    {
        await using var decompressionStream = LZ4Stream.Decode(sourceStream);
        await decompressionStream.CopyToAsync(outputStream);
    }
}
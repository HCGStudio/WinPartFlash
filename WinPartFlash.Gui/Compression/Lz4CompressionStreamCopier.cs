using System.IO;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Streams;

namespace WinPartFlash.Gui.Compression;

public class Lz4CompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(Stream sourceStream, Stream outputStream)
    {
        await using var compressionStream = LZ4Stream.Encode(outputStream);
        await sourceStream.CopyToAsync(compressionStream);
    }
}
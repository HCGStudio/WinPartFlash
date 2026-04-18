using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using WinPartFlash.Gui.Utils;

namespace WinPartFlash.Gui.Compression;

public class Lz4CompressionStreamCopier : ICompressionStreamCopier
{
    public async ValueTask CopyToStreamAsync(
        Stream sourceStream,
        Stream outputStream,
        CompressionOptions options = default,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = new LZ4EncoderSettings { CompressionLevel = MapLevel(options.Level) };
        await using var compressionStream = LZ4Stream.Encode(outputStream, settings, leaveOpen: true);
        await StreamCopyHelper.CopyAsync(sourceStream, compressionStream, progress, cancellationToken);
    }

    private static LZ4Level MapLevel(int? level) => level switch
    {
        null or <= 1 => LZ4Level.L00_FAST,
        2 or 3 => LZ4Level.L03_HC,
        4 => LZ4Level.L04_HC,
        5 => LZ4Level.L05_HC,
        6 => LZ4Level.L06_HC,
        7 => LZ4Level.L07_HC,
        8 => LZ4Level.L08_HC,
        9 => LZ4Level.L09_HC,
        10 => LZ4Level.L10_OPT,
        11 => LZ4Level.L11_OPT,
        _ => LZ4Level.L12_MAX
    };
}

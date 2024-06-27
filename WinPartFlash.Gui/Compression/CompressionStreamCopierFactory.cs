using System;
using Microsoft.Extensions.DependencyInjection;

namespace WinPartFlash.Gui.Compression;

public class CompressionStreamCopierFactory(IServiceProvider serviceProvider) : ICompressionStreamCopierFactory
{
    public ICompressionStreamCopier GetCopier(CompressionType compressionType)
    {
        return compressionType switch
        {
            CompressionType.Raw => serviceProvider.GetRequiredService<RawStreamCopier>(),
            CompressionType.GzipCompress => serviceProvider.GetRequiredService<GzipCompressionStreamCopier>(),
            CompressionType.GzipDecompress => serviceProvider.GetRequiredService<GzipDecompressionStreamCopier>(),
            CompressionType.Lz4Compress => serviceProvider.GetRequiredService<Lz4CompressionStreamCopier>(),
            CompressionType.Lz4Decompress => serviceProvider.GetRequiredService<Lz4DecompressionStreamCopier>(),
            CompressionType.XzDecompress => serviceProvider.GetRequiredService<XzDecompressionStreamCopier>(),
            CompressionType.ZstandardCompress => serviceProvider.GetRequiredService<ZstandardCompressionStreamCopier>(),
            CompressionType.ZstandardDecompress => serviceProvider
                .GetRequiredService<ZstandardDecompressionStreamCopier>(),
            _ => throw new ArgumentOutOfRangeException(nameof(compressionType), compressionType, null)
        };
    }
}
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
            CompressionType.GzipDecompress => serviceProvider.GetRequiredService<GzipDeCompressionStreamCopier>(),
            // TODO: Support following methods
            CompressionType.Lz4Compress => throw new NotSupportedException(),
            CompressionType.Lz4Decompress => throw new NotSupportedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(compressionType), compressionType, null)
        };
    }
}
using Microsoft.Extensions.DependencyInjection;

namespace WinPartFlash.Gui.Compression;

public static class CompressionStreamCopierExtension
{
    public static IServiceCollection AddCompressionStreamCopier(this IServiceCollection services)
    {
        services.AddSingleton<RawStreamCopier>();
        services.AddSingleton<GzipCompressionStreamCopier>();
        services.AddSingleton<GzipDecompressionStreamCopier>();
        services.AddSingleton<XzDecompressionStreamCopier>();
        services.AddSingleton<ZstandardCompressionStreamCopier>();
        services.AddSingleton<ZstandardDecompressionStreamCopier>();
        services.AddSingleton<Lz4CompressionStreamCopier>();
        services.AddSingleton<Lz4DecompressionStreamCopier>();
        services.AddSingleton<ICompressionStreamCopierFactory, CompressionStreamCopierFactory>();
        return services;
    }
}
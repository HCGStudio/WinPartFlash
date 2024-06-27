using Microsoft.Extensions.DependencyInjection;

namespace WinPartFlash.Gui.Compression;

public static class CompressionStreamCopierExtension
{
    public static IServiceCollection AddCompressionStreamCopier(this IServiceCollection services)
    {
        services.AddSingleton<RawStreamCopier>();
        services.AddSingleton<GzipCompressionStreamCopier>();
        services.AddSingleton<GzipDeCompressionStreamCopier>();
        services.AddSingleton<ICompressionStreamCopierFactory, CompressionStreamCopierFactory>();
        return services;
    }
}
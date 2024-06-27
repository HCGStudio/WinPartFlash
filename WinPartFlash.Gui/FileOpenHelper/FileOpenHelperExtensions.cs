using Microsoft.Extensions.DependencyInjection;

namespace WinPartFlash.Gui.FileOpenHelper;

public static class FileOpenHelperExtensions
{
    public static IServiceCollection AddFileOpenHelper(this IServiceCollection services)
    {
        services.AddScoped<IFileOpenHelper, FileOpenHelper>();
        services.AddHttpClient(nameof(FileOpenHelper),
            client => { client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.7.1"); });
        return services;
    }
}
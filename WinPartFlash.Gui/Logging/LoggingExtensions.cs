using Microsoft.Extensions.DependencyInjection;

namespace WinPartFlash.Gui.Logging;

public static class LoggingExtensions
{
    public static IServiceCollection AddLogSink(this IServiceCollection services)
    {
        services.AddSingleton<ILogSink, LogSink>();
        return services;
    }
}

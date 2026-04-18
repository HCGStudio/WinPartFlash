using Microsoft.Extensions.DependencyInjection;

namespace WinPartFlash.Gui.Diagnostics;

public static class DiagnosticsExtensions
{
    public static IServiceCollection AddDiagnostics(this IServiceCollection services)
    {
        services.AddSingleton<ISystemInfoProvider, SystemInfoProvider>();
        return services;
    }
}

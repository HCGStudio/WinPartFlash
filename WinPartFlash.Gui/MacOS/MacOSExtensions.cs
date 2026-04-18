using System;
using Microsoft.Extensions.DependencyInjection;

namespace WinPartFlash.Gui.MacOS;

public static class MacOSExtensions
{
    /// <summary>
    /// Registers the macOS privileged-disk gateway and its factory.  Safe to
    /// call on any platform — it's a no-op when not running on macOS.  The
    /// gateway itself is OS-guarded internally so that DI graph composition
    /// does not throw on Linux/Windows.
    /// </summary>
    public static IServiceCollection AddMacOSPrivileges(this IServiceCollection services)
    {
        if (!OperatingSystem.IsMacOS()) return services;

        services.AddSingleton<MacOSPrivilegedDiskGatewayFactory>();
        return services;
    }
}

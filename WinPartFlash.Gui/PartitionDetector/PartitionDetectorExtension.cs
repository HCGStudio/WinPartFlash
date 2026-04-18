using System;
using Microsoft.Extensions.DependencyInjection;

namespace WinPartFlash.Gui.PartitionDetector;

public static class PartitionDetectorExtension
{
    public static IServiceCollection AddPartitionDetector(this IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IPartitionDetector, WindowsPartitionDetector>();
            services.AddSingleton<IDiskEjector, WindowsDiskEjector>();
        }
        else if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IPartitionDetector, LinuxPartitionDetector>();
            services.AddSingleton<IDiskEjector, LinuxDiskEjector>();
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IPartitionDetector, MacOSPartitionDetector>();
            services.AddSingleton<IDiskEjector, MacOSDiskEjector>();
        }
        // ^ macOS detector requires MacOSPrivilegedDiskGatewayFactory; that
        // service is registered by AddMacOSPrivileges() in App startup.
        else
            throw new NotSupportedException("The app is not supported on this platform.");

        return services;
    }
}
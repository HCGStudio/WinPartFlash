using System;
using Microsoft.Extensions.DependencyInjection;

namespace WinPartFlash.Gui.PartitionDetector;

public static class PartitionDetectorExtension
{
    public static IServiceCollection AddPartitionDetector(this IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
            services.AddSingleton<IPartitionDetector, WindowsPartitionDetector>();
        else if (OperatingSystem.IsLinux())
            services.AddSingleton<IPartitionDetector, LinuxPartitionDetector>();
        else if (OperatingSystem.IsMacOS())
            services.AddSingleton<IPartitionDetector, MacOsPartitionDetector>();
        else
            throw new NotSupportedException("The app is not supported on this platform.");

        return services;
    }
}
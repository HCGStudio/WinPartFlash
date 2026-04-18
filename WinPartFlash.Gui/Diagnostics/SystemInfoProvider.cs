using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using WinPartFlash.Gui.MacOS;
using WinPartFlash.Gui.Resources;

namespace WinPartFlash.Gui.Diagnostics;

public sealed class SystemInfoProvider : ISystemInfoProvider
{
    private const string NativeLibraryName = "libWinPartFlashLib.dylib";

    private readonly IServiceProvider _services;
    private SystemInfoSnapshot? _cached;

    public SystemInfoProvider(IServiceProvider services)
    {
        _services = services;
    }

    public SystemInfoSnapshot GetSnapshot()
    {
        return _cached ??= BuildSnapshot();
    }

    private SystemInfoSnapshot BuildSnapshot()
    {
        var entry = Assembly.GetEntryAssembly();
        var version = entry?.GetName().Version?.ToString() ?? "0.0.0";
        var runtime = RuntimeInformation.FrameworkDescription;
        var os = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

        return new(
            AppVersion: version,
            Runtime: runtime,
            OperatingSystem: os,
            NativeLibraryStatus: DescribeNativeLibrary(),
            PrivilegedHelperStatus: DescribePrivilegedHelper());
    }

    private static string DescribeNativeLibrary()
    {
        if (!OperatingSystem.IsMacOS())
            return Strings.StatusNotApplicable;

        var candidate = Path.Combine(AppContext.BaseDirectory, NativeLibraryName);
        return File.Exists(candidate)
            ? candidate
            : Strings.StatusUnavailable;
    }

    private string DescribePrivilegedHelper()
    {
        if (!OperatingSystem.IsMacOS())
            return Strings.StatusNotApplicable;

        var factory = _services.GetService<MacOSPrivilegedDiskGatewayFactory>();
        if (factory == null)
            return Strings.StatusUnavailable;

        try
        {
            var gateway = factory.GetAsync().GetAwaiter().GetResult();
            return gateway switch
            {
                SmAppServicePrivilegedDiskGateway => Strings.StatusAvailable,
                OsascriptPrivilegedDiskGateway => Strings.InfoPrivilegedFallbackInUse,
                _ => Strings.StatusAvailable
            };
        }
        catch
        {
            return Strings.StatusUnavailable;
        }
    }
}

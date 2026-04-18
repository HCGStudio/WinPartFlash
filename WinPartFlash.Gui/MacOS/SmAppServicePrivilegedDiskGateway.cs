using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WinPartFlash.Gui.MacOS;

/// <summary>
/// Production gateway: registers a LaunchDaemon via SMAppService and talks to
/// it over an XPC Mach service.  Scaffolded; activation requires a signed
/// bundle (see file-level comments in the prior revision).  On unsigned
/// builds <see cref="TryRegisterAsync"/> returns false and the factory falls
/// back to <see cref="OsascriptPrivilegedDiskGateway"/>.
/// </summary>
[SupportedOSPlatform("MacOS")]
public sealed partial class SmAppServicePrivilegedDiskGateway : IPrivilegedDiskGateway
{
    public const string DaemonPlistName = "com.hcgstudio.winpartflash.helper.plist";

    private readonly ILogger<SmAppServicePrivilegedDiskGateway> _logger;

    public SmAppServicePrivilegedDiskGateway(ILogger<SmAppServicePrivilegedDiskGateway> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attempts to register the daemon via SMAppService.  Returns true only
    /// when the runtime mechanism is genuinely available (signed bundle +
    /// successful registration).  Called once at startup by the factory.
    /// </summary>
    public static Task<bool> TryRegisterAsync(
        ILogger<SmAppServicePrivilegedDiskGateway> logger,
        CancellationToken cancellationToken)
    {
        // We deliberately do NOT attempt the Obj-C runtime dance on unsigned
        // builds — it would always fail with errSecCSUnsigned and the failure
        // is not actionable for the user.  When a signed pipeline lands,
        // replace this with the real SMAppService.daemon(plistName:).register().
        _ = cancellationToken;
        LogRegistrationSkipped(logger, DaemonPlistName);
        return Task.FromResult(false);
    }

    public Task<Stream> OpenPartitionAsync(
        string device, ulong offset, ulong length, FileAccess access, CancellationToken cancellationToken)
    {
        LogOpenPartitionRefused(device);
        throw new PrivilegedHelperUnavailableException(
            "SMAppService daemon is not registered. This build is not signed; the osascript fallback should be used instead.");
    }

    public Task UnmountDiskAsync(string device, CancellationToken cancellationToken)
        => MacOSDiskUtil.UnmountDiskAsync(device, cancellationToken);

    [LoggerMessage(EventId = 100, Level = LogLevel.Debug,
        Message = "SMAppService registration skipped for {PlistName}; build is unsigned.")]
    private static partial void LogRegistrationSkipped(ILogger logger, string plistName);

    [LoggerMessage(EventId = 101, Level = LogLevel.Warning,
        Message = "SMAppService gateway invoked for {Device} but daemon is not registered.")]
    private partial void LogOpenPartitionRefused(string device);
}

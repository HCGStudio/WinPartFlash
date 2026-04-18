using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WinPartFlash.Gui.MacOS;

/// <summary>
/// Picks the best privileged-disk gateway available at runtime.  Prefers
/// SMAppService LaunchDaemon (one-time auth, code-requirement enforced) when
/// the bundle is signed; falls back to per-op `osascript` elevation
/// otherwise.  The choice is cached for the process lifetime.
/// </summary>
[SupportedOSPlatform("MacOS")]
public sealed partial class MacOSPrivilegedDiskGatewayFactory
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<MacOSPrivilegedDiskGatewayFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private IPrivilegedDiskGateway? _cached;

    public MacOSPrivilegedDiskGatewayFactory(
        ILogger<MacOSPrivilegedDiskGatewayFactory> logger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<IPrivilegedDiskGateway> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null) return _cached;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null) return _cached;

            var smLogger = _loggerFactory.CreateLogger<SmAppServicePrivilegedDiskGateway>();
            if (await SmAppServicePrivilegedDiskGateway.TryRegisterAsync(smLogger, cancellationToken))
            {
                LogSelectedSmAppService();
                _cached = new SmAppServicePrivilegedDiskGateway(smLogger);
            }
            else
            {
                LogSelectedOsascript();
                _cached = new OsascriptPrivilegedDiskGateway(
                    _loggerFactory.CreateLogger<OsascriptPrivilegedDiskGateway>());
            }

            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Privileged gateway selected: SMAppService LaunchDaemon (signed build).")]
    private partial void LogSelectedSmAppService();

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Privileged gateway selected: osascript fallback (per-op admin prompt).")]
    private partial void LogSelectedOsascript();
}

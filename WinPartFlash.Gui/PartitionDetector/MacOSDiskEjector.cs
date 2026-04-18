using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using WinPartFlash.Gui.MacOS;

namespace WinPartFlash.Gui.PartitionDetector;

[SupportedOSPlatform("MacOS")]
public sealed class MacOSDiskEjector : IDiskEjector
{
    public Task EjectAsync(string diskDeviceId, CancellationToken cancellationToken = default)
        => MacOSDiskUtil.EjectDiskAsync(diskDeviceId, cancellationToken);
}

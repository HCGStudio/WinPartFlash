using System.Threading;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.PartitionDetector;

public interface IDiskEjector
{
    Task EjectAsync(string diskDeviceId, CancellationToken cancellationToken = default);
}

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.MacOS;

/// <summary>
/// Boundary between the unprivileged Avalonia GUI and whatever macOS
/// mechanism actually performs raw block-device I/O as root.  Implementations
/// must keep the privilege scope as narrow as possible: open exactly the
/// requested device + range, with the requested access, and nothing else.
/// </summary>
public interface  IPrivilegedDiskGateway
{
    /// <summary>
    /// Returns a disposable Stream that maps onto [offset, offset+length) of
    /// the given raw block device.  Disposal terminates the privileged
    /// helper.  Each call may trigger one authorization prompt.
    /// </summary>
    Task<Stream> OpenPartitionAsync(
        string device,
        ulong offset,
        ulong length,
        FileAccess access,
        CancellationToken cancellationToken);

    /// <summary>
    /// Best-effort unmount via diskutil so the device is not held by Finder
    /// before a write.  Non-privileged on macOS for user-mounted volumes.
    /// </summary>
    Task UnmountDiskAsync(string device, CancellationToken cancellationToken);
}

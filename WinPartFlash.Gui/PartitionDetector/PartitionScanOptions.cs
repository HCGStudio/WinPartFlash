namespace WinPartFlash.Gui.PartitionDetector;

public record PartitionScanOptions(bool WholeDiskMode = false, bool ProtectSystemDisk = true)
{
    public static PartitionScanOptions Default { get; } = new();
}

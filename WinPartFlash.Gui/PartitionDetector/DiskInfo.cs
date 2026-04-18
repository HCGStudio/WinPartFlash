namespace WinPartFlash.Gui.PartitionDetector;

public record DiskInfo(string Name, ulong SectorSize, ulong TotalSize = 0, bool IsSystem = false);

using System;
using System.IO;

namespace WinPartFlash.Gui.PartitionDetector;

public record PartitionResult(
    string Name,
    ulong Length,
    Func<Stream> OpenFileStream,
    string? DiskDeviceId = null,
    bool IsWholeDisk = false,
    bool IsSystemDisk = false);

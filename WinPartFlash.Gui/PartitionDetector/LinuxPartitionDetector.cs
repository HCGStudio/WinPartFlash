using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;

namespace WinPartFlash.Gui.PartitionDetector;

[SupportedOSPlatform("Linux")]
public class LinuxPartitionDetector : GuidPartitionTableBasedPartitionDetector
{
    protected override IList<DiskInfo> GetDisks()
    {
        var result = new List<DiskInfo>();

        foreach (var directory in Directory.EnumerateDirectories("/sys/class/block"))
        {
            var sizeFile = Path.Combine(directory, "size");
            var sectorFile = Path.Combine(directory, "queue/hw_sector_size");
            var blockFilePath = Path.Combine("/dev", Path.GetFileName(directory));
            if (File.Exists(sizeFile) &&
                ulong.TryParse(File.ReadAllBytes(sizeFile), out var size) &&
                size > 0 &&
                File.Exists(sectorFile) &&
                ulong.TryParse(File.ReadAllBytes(sectorFile), out var sectorSize) &&
                sectorSize > 0 &&
                File.Exists(blockFilePath))
                result.Add(new DiskInfo(blockFilePath, sectorSize));
        }

        return result;
    }
}
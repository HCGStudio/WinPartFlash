using System;
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
        var systemDisk = TryResolveSystemDisk();

        foreach (var directory in Directory.EnumerateDirectories("/sys/class/block"))
        {
            var sizeFile = Path.Combine(directory, "size");
            var sectorFile = Path.Combine(directory, "queue/hw_sector_size");
            var name = Path.GetFileName(directory);
            var blockFilePath = Path.Combine("/dev", name);
            if (File.Exists(sizeFile) &&
                ulong.TryParse(File.ReadAllText(sizeFile).Trim(), out var sectors) &&
                sectors > 0 &&
                File.Exists(sectorFile) &&
                ulong.TryParse(File.ReadAllText(sectorFile).Trim(), out var sectorSize) &&
                sectorSize > 0 &&
                File.Exists(blockFilePath))
            {
                var totalSize = sectors * 512UL; // /sys size is in 512-byte units
                var isSystem = !string.IsNullOrEmpty(systemDisk) &&
                               string.Equals(blockFilePath, systemDisk, StringComparison.Ordinal);
                result.Add(new(blockFilePath, sectorSize, totalSize, isSystem));
            }
        }

        return result;
    }

    private static string? TryResolveSystemDisk()
    {
        try
        {
            string? rootDev = null;
            foreach (var line in File.ReadAllLines("/proc/mounts"))
            {
                var parts = line.Split(' ');
                if (parts.Length >= 2 && parts[1] == "/")
                {
                    rootDev = parts[0];
                    break;
                }
            }
            if (rootDev is null || !rootDev.StartsWith("/dev/")) return null;

            var devName = Path.GetFileName(rootDev);
            var sysBlock = $"/sys/class/block/{devName}";
            if (!Directory.Exists(sysBlock)) return null;

            // If this is a partition, /sys/class/block/<part> is a symlink like
            // .../sdX/sdXN — the parent directory's name is the whole-disk name.
            var real = new DirectoryInfo(sysBlock);
            var parentName = real.Parent?.Name;
            if (!string.IsNullOrEmpty(parentName) && parentName != "block")
                return "/dev/" + parentName;
            return "/dev/" + devName;
        }
        catch
        {
            return null;
        }
    }
}

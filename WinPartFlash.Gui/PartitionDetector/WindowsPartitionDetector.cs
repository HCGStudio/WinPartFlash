using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;

namespace WinPartFlash.Gui.PartitionDetector;

[SupportedOSPlatform("Windows")]
public class WindowsPartitionDetector : GuidPartitionTableBasedPartitionDetector
{
    protected override IList<DiskInfo> GetDisks()
    {
        var result = new List<DiskInfo>();

        var systemDiskNumber = TryGetSystemDiskNumber();

        var scope = new ManagementScope(@"\\localhost\ROOT\Microsoft\Windows\Storage");
        var query = new ObjectQuery("SELECT * FROM MSFT_PhysicalDisk");
        var searcher = new ManagementObjectSearcher(scope, query);
        var data = searcher.Get();
        foreach (var disk in data)
        {
            var deviceId = disk["DeviceId"].ToString() ?? string.Empty;
            var sectorSize = Convert.ToUInt32(disk["LogicalSectorSize"]);
            ulong size = 0;
            try { size = Convert.ToUInt64(disk["Size"]); } catch { }
            var isSystem = systemDiskNumber.HasValue &&
                           int.TryParse(deviceId, out var n) &&
                           n == systemDiskNumber.Value;
            result.Add(new(deviceId, sectorSize, size, isSystem));
        }

        return result;
    }

    private static int? TryGetSystemDiskNumber()
    {
        try
        {
            var sysRoot = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrEmpty(sysRoot)) return null;
            var sysLetter = sysRoot.TrimEnd('\\', '/').TrimEnd(':');
            if (string.IsNullOrEmpty(sysLetter)) return null;

            var scope = new ManagementScope(@"\\localhost\ROOT\Microsoft\Windows\Storage");
            var q = new ObjectQuery(
                $"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '{sysLetter}'");
            using var searcher = new ManagementObjectSearcher(scope, q);
            var first = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
            if (first?["DiskNumber"] is { } n) return Convert.ToInt32(n);
        }
        catch
        {
        }
        return null;
    }
}

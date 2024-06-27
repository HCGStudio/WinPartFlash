using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.Versioning;

namespace WinPartFlash.Gui.PartitionDetector;

[SupportedOSPlatform("Windows")]
public class WindowsPartitionDetector : GuidPartitionTableBasedPartitionDetector
{
    protected override IList<DiskInfo> GetDisks()
    {
        var result = new List<DiskInfo>();

        var scope = new ManagementScope(@"\\localhost\ROOT\Microsoft\Windows\Storage");
        var query = new ObjectQuery("SELECT * FROM MSFT_PhysicalDisk");
        var searcher = new ManagementObjectSearcher(scope, query);
        var data = searcher.Get();
        foreach (var disk in data)
            result.Add(new DiskInfo(
                disk["DeviceId"].ToString() ?? string.Empty,
                Convert.ToUInt32(disk["LogicalSectorSize"])));

        return result;
    }
}
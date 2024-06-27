using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace WinPartFlash.Gui.PartitionDetector;

[SupportedOSPlatform("MacOS")]
public partial class MacOsPartitionDetector : GuidPartitionTableBasedPartitionDetector
{
    [LibraryImport(
        "WinPartFlashLib",
        EntryPoint = "getBlockSize",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf8)]
    private static partial int GetSectorSize(string name, out ulong size);

    [GeneratedRegex(@"^/dev/disk\d+$")]
    private static partial Regex DeviceDeviceMatchRegex();

    protected override IList<DiskInfo> GetDisks()
    {
        var list = new List<DiskInfo>();
        var regex = DeviceDeviceMatchRegex();

        foreach (var device in Directory
                     .GetFiles("/dev")
                     .SelectMany(name => regex.IsMatch(name) ? [name] : Array.Empty<string>())
                     .ToArray())
            if (GetSectorSize(device, out var size) != -1)
                list.Add(new DiskInfo(device, size));

        return list;
    }
}
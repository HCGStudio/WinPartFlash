using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WinPartFlash.Gui.MacOS;

/// <summary>
/// Thin wrapper around `diskutil`.  All operations here are non-privileged
/// for normal user-visible disks: enumerating disks, reading their size,
/// listing partitions, and unmounting user-mounted volumes do NOT require
/// root.  This is the path used to populate the partition list in the GUI
/// without ever prompting for a password.
/// </summary>
[SupportedOSPlatform("MacOS")]
public static partial class MacOSDiskUtil
{
    [GeneratedRegex(@"^disk\d+$")]
    private static partial Regex WholeDiskRegex();

    public sealed record DiskUtilPartition(
        string Device,
        ulong Offset,
        ulong Length,
        ulong SectorSize,
        string DisplayName,
        string WholeDiskId,
        bool IsWholeDisk,
        bool IsSystemDisk);

    public static async Task<string?> GetSystemWholeDiskIdAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var plist = await RunDiskutilAsync(["info", "-plist", "/"], cancellationToken);
            var dict = ParsePlistRootDict(plist);
            var parent = ReadString(dict, "ParentWholeDisk");
            if (!string.IsNullOrEmpty(parent)) return parent;
            return ReadString(dict, "DeviceIdentifier");
        }
        catch
        {
            return null;
        }
    }

    public static async Task EjectDiskAsync(string wholeDiskId, CancellationToken cancellationToken = default)
    {
        await RunDiskutilAsync(["eject", wholeDiskId.StartsWith("/dev/") ? wholeDiskId : "/dev/" + wholeDiskId],
            cancellationToken);
    }

    public static async Task<IReadOnlyList<DiskUtilPartition>> ListAllPartitionsAsync(
        bool wholeDiskMode,
        bool protectSystemDisk,
        CancellationToken cancellationToken = default)
    {
        var plist = await RunDiskutilAsync(["list", "-plist", "physical"], cancellationToken);
        var rootDict = ParsePlistRootDict(plist);
        var systemDisk = await GetSystemWholeDiskIdAsync(cancellationToken);
        var result = new List<DiskUtilPartition>();

        foreach (var disk in EnumerateDictArray(rootDict, "AllDisksAndPartitions"))
        {
            var diskId = ReadString(disk, "DeviceIdentifier");
            if (string.IsNullOrEmpty(diskId) || !WholeDiskRegex().IsMatch(diskId))
                continue;

            var diskInfo = await GetDiskInfoAsync("/dev/" + diskId, cancellationToken);
            if (diskInfo.SectorSize == 0)
                continue;

            var isSystem = !string.IsNullOrEmpty(systemDisk) && diskId == systemDisk;
            if (protectSystemDisk && isSystem)
                continue;

            if (wholeDiskMode)
            {
                if (diskInfo.Size == 0) continue;
                result.Add(new(
                    Device: "/dev/r" + diskId,
                    Offset: 0,
                    Length: diskInfo.Size,
                    SectorSize: diskInfo.SectorSize,
                    DisplayName: string.Format(Resources.Strings.PartitionNameWholeDisk, "/dev/" + diskId),
                    WholeDiskId: diskId,
                    IsWholeDisk: true,
                    IsSystemDisk: isSystem));
                continue;
            }

            foreach (var part in EnumerateDictArray(disk, "Partitions"))
            {
                var partId = ReadString(part, "DeviceIdentifier");
                if (string.IsNullOrEmpty(partId))
                    continue;

                var partInfo = await GetDiskInfoAsync("/dev/" + partId, cancellationToken);
                if (partInfo.Size == 0)
                    continue;

                var name = ReadString(part, "VolumeName");
                if (string.IsNullOrEmpty(name))
                    name = ReadString(part, "Content") ?? partId;

                // Use the raw character device for unbuffered, byte-aligned I/O.
                var device = "/dev/r" + diskId;

                result.Add(new(
                    Device: device,
                    Offset: partInfo.Offset,
                    Length: partInfo.Size,
                    SectorSize: diskInfo.SectorSize,
                    DisplayName: string.Format(Resources.Strings.PartitionNameDiskPartition, "/dev/" + diskId, partId, name),
                    WholeDiskId: diskId,
                    IsWholeDisk: false,
                    IsSystemDisk: isSystem));
            }
        }

        return result;
    }

    public static async Task UnmountDiskAsync(string device, CancellationToken cancellationToken)
    {
        // diskutil unmountDisk does not require root for user-mounted volumes;
        // failure is non-fatal — the helper will surface EBUSY clearly if the
        // device is still held when we try to write.
        try
        {
            await RunDiskutilAsync(["unmountDisk", device], cancellationToken);
        }
        catch
        {
            // intentional swallow
        }
    }

    private sealed record DiskInfo(ulong Size, ulong Offset, ulong SectorSize);

    private static async Task<DiskInfo> GetDiskInfoAsync(string device, CancellationToken cancellationToken)
    {
        try
        {
            var plist = await RunDiskutilAsync(["info", "-plist", device], cancellationToken);
            var dict = ParsePlistRootDict(plist);
            return new(
                Size: ReadUInt64(dict, "Size"),
                Offset: ReadUInt64(dict, "PartitionMapPartitionOffset"),
                SectorSize: ReadUInt64(dict, "DeviceBlockSize"));
        }
        catch
        {
            return new(0, 0, 0);
        }
    }

    private static async Task<string> RunDiskutilAsync(string[] args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("/usr/sbin/diskutil")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)
                        ?? throw new InvalidOperationException("Failed to spawn diskutil.");

        var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
        await proc.WaitForExitAsync(cancellationToken);

        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"diskutil {string.Join(' ', args)} exited {proc.ExitCode}: {stderr}");
        return stdout;
    }

    // ---- Tiny inline plist reader -------------------------------------------
    // We only need scalar string/integer lookups inside a dict, plus walking an
    // `<array><dict>...</dict></array>` value.  XLinq is enough.

    private static XElement ParsePlistRootDict(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidOperationException("Empty plist.");
        var dict = root.Element("dict")
                  ?? throw new InvalidOperationException("Plist root is not a dict.");
        return dict;
    }

    private static IEnumerable<XElement> EnumerateDictArray(XElement dict, string key)
    {
        var arr = ValueElement(dict, key);
        if (arr is null || arr.Name != "array") yield break;
        foreach (var d in arr.Elements("dict"))
            yield return d;
    }

    private static string? ReadString(XElement dict, string key)
    {
        var v = ValueElement(dict, key);
        return v?.Name == "string" ? v.Value : null;
    }

    private static ulong ReadUInt64(XElement dict, string key)
    {
        var v = ValueElement(dict, key);
        return v?.Name == "integer" && ulong.TryParse(v.Value, out var n) ? n : 0;
    }

    private static XElement? ValueElement(XElement dict, string key)
    {
        // Walk children pairwise: <key>name</key><value>...</value>
        XElement? prevKey = null;
        foreach (var child in dict.Elements())
        {
            if (prevKey is not null)
            {
                if (prevKey.Value == key) return child;
                prevKey = null;
            }
            if (child.Name == "key") prevKey = child;
        }
        return null;
    }
}

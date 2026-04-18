using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using WinPartFlash.Gui.GuidPartition;
using WinPartFlash.Gui.Resources;
using WinPartFlash.Gui.Utils;

namespace WinPartFlash.Gui.PartitionDetector;

public abstract class GuidPartitionTableBasedPartitionDetector : IPartitionDetector
{
    private static unsafe int CountStringLength(char* s, int maxCount)
    {
        for (var i = 0; i < maxCount; i++)
            if (s[i] == '\0')
                return i;

        return 35;
    }

    public unsafe IList<PartitionResult> DetectPartitions(PartitionScanOptions options)
    {
        var result = new List<PartitionResult>();
        var disks = GetDisks();

        var buffer = stackalloc byte[8192];

        foreach (var diskInfo in disks)
        {
            if (options.ProtectSystemDisk && diskInfo.IsSystem)
                continue;

            if (options.WholeDiskMode)
            {
                var length = diskInfo.TotalSize > 0
                    ? diskInfo.TotalSize
                    : TryGetLengthFromFile(diskInfo.Name);
                if (length == 0) continue;
                result.Add(new(
                    string.Format(Strings.PartitionNameWholeDisk, diskInfo.Name),
                    length,
                    OpenWholeDisk(diskInfo.Name),
                    DiskDeviceId: diskInfo.Name,
                    IsWholeDisk: true,
                    IsSystemDisk: diskInfo.IsSystem));
                continue;
            }

            try
            {
                using var diskFile = File.Open(
                    diskInfo.Name,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite);

                var bufferSpan = new Span<byte>(buffer, (int)diskInfo.SectorSize);
                diskFile.Seek((long)diskInfo.SectorSize, SeekOrigin.Begin);

                if (diskFile.Read(bufferSpan) != (int)diskInfo.SectorSize)
                    continue;

                var entries = VerifyDiskAndProcessHeader(
                    (GuidPartitionTableHeader*)buffer,
                    diskFile,
                    diskInfo);

                if (entries == null) continue;
                // TODO: Log this entries null

                var span = entries.AsSpan();
                for (var index = 0; index < span.Length; index++)
                {
                    ref var entry = ref span[index];
                    if (entry.PartitionGuid == Guid.Empty)
                        continue;

                    fixed (char* s = entry.PartitionName)
                    {
                        var partitionName = new Span<char>(s, CountStringLength(s, 36));
                        var partitionLength = diskInfo.SectorSize * (entry.EndLba - entry.StartLba + 1);
                        var partitionOffset = diskInfo.SectorSize * entry.StartLba;
                        result.Add(new(
                            string.Format(Strings.PartitionNameDiskPartition, diskInfo.Name, index + 1, partitionName.ToString()),
                            partitionLength,
                            OpenAndSeekLength(diskInfo.Name, partitionOffset, partitionLength),
                            DiskDeviceId: diskInfo.Name,
                            IsWholeDisk: false,
                            IsSystemDisk: diskInfo.IsSystem));
                    }
                }
            }
            catch (Exception)
            {
                // TODO: Add log here
            }

        }

        return result;
    }

    protected abstract IList<DiskInfo> GetDisks();

    private static ulong TryGetLengthFromFile(string fileName)
    {
        try
        {
            using var f = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return (ulong)f.Length;
        }
        catch
        {
            return 0;
        }
    }

    private Func<Stream> OpenWholeDisk(string fileName)
    {
        return () => File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
    }

    private Func<Stream> OpenAndSeekLength(string fileName, ulong offset, ulong length)
    {
        return () =>
        {
            var diskFile = File.Open(
                fileName,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);

            return new SubStream(diskFile, (long)offset, (long)length);
        };
    }

    private unsafe GuidPartitionEntry[]? VerifyDiskAndProcessHeader(
        GuidPartitionTableHeader* header,
        FileStream diskFile,
        DiskInfo diskInfo)
    {
        if (header->Signature != GuidPartitionTableHelper.CorrectSignature)
            return null;

        if (header->Revision != GuidPartitionTableHelper.SupportedRevision)
            return null;

        if (header->ReservedZero != 0)
            return null;

        if (header->HeaderSize != Marshal.SizeOf<GuidPartitionTableHeader>())
            return null;

        if (header->PartitionEntrySize != Marshal.SizeOf<GuidPartitionEntry>())
            return null;

        var crc = header->CrcValue;
        header->CrcValue = 0;

        var calculatedCrc = Crc32.Compute(new Span<byte>(header, (int)header->HeaderSize));
        if (crc != calculatedCrc)
            return null;
        header->CrcValue = crc;

        return VerifyAndProcessPartitionEntry(header, diskFile, diskInfo);
    }

    private unsafe GuidPartitionEntry[]? VerifyAndProcessPartitionEntry(
        GuidPartitionTableHeader* header,
        FileStream diskFile,
        DiskInfo diskInfo)
    {
        var buffer = stackalloc byte[(int)diskInfo.SectorSize];
        var entries = (GuidPartitionEntry*)buffer;
        var bufferSpan = new Span<byte>(buffer, (int)diskInfo.SectorSize);

        var crc = 0u;

        var arr = new GuidPartitionEntry[header->PartitionEntriesCount];
        var entryIndex = 0;


        for (var i = header->PartitionEntriesStartLba; i < header->FirstUsableLba; i++)
        {
            diskFile.Seek((long)(diskInfo.SectorSize * i), SeekOrigin.Begin);

            if (diskFile.Read(bufferSpan) != (int)diskInfo.SectorSize)
                return null;

            for (var j = 0u; j < diskInfo.SectorSize / header->PartitionEntrySize; j++, entryIndex++)
                arr[entryIndex] = entries[j];

            crc = Crc32.Compute(bufferSpan, crc);
        }

        return header->PartitionEntriesCrc != crc ? null : arr;
    }
}

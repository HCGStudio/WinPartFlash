using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using WinPartFlash.Gui.GuidPartition;
using WinPartFlash.Gui.Utils;

namespace WinPartFlash.Gui.Inspection;

public record GuidPartitionTableSnapshot(
    GuidPartitionTableHeader Header,
    IReadOnlyList<GuidPartitionEntry> Entries,
    uint SectorSize);

public static class GuidPartitionTableReader
{
    private static readonly uint[] CandidateSectorSizes = [512, 4096];

    public static GuidPartitionTableSnapshot? TryRead(Stream stream)
    {
        foreach (var sector in CandidateSectorSizes)
        {
            if (TryReadAt(stream, sector) is { } snap)
                return snap;
        }
        return null;
    }

    private static unsafe GuidPartitionTableSnapshot? TryReadAt(Stream stream, uint sectorSize)
    {
        if (!stream.CanSeek) return null;
        if (stream.Length < sectorSize * 3) return null;

        var headerBuf = new byte[sectorSize];
        stream.Seek(sectorSize, SeekOrigin.Begin);
        if (!ReadExact(stream, headerBuf)) return null;

        GuidPartitionTableHeader header;
        fixed (byte* p = headerBuf)
        {
            header = *(GuidPartitionTableHeader*)p;
            if (header.Signature != GuidPartitionTableHelper.CorrectSignature) return null;
            if (header.Revision != GuidPartitionTableHelper.SupportedRevision) return null;
            if (header.HeaderSize != Marshal.SizeOf<GuidPartitionTableHeader>()) return null;
            if (header.PartitionEntrySize != Marshal.SizeOf<GuidPartitionEntry>()) return null;

            var savedCrc = header.CrcValue;
            var hdr = (GuidPartitionTableHeader*)p;
            hdr->CrcValue = 0;
            var computed = Crc32.Compute(new System.Span<byte>(p, (int)header.HeaderSize));
            hdr->CrcValue = savedCrc;
            if (savedCrc != computed) return null;
        }

        var entries = new List<GuidPartitionEntry>((int)header.PartitionEntriesCount);
        var entryBytes = header.PartitionEntriesCount * header.PartitionEntrySize;
        var bufLen = ((entryBytes + sectorSize - 1) / sectorSize) * sectorSize;
        var entryBuf = new byte[bufLen];
        stream.Seek((long)(header.PartitionEntriesStartLba * sectorSize), SeekOrigin.Begin);
        if (!ReadExact(stream, entryBuf)) return null;

        fixed (byte* p = entryBuf)
        {
            var computedCrc = Crc32.Compute(new System.Span<byte>(p, (int)entryBytes));
            if (computedCrc != header.PartitionEntriesCrc) return null;

            var arr = (GuidPartitionEntry*)p;
            for (var i = 0u; i < header.PartitionEntriesCount; i++)
                entries.Add(arr[i]);
        }

        return new(header, entries, sectorSize);
    }

    private static bool ReadExact(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var n = stream.Read(buffer, offset, buffer.Length - offset);
            if (n <= 0) return false;
            offset += n;
        }
        return true;
    }
}

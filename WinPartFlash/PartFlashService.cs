using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace WinPartFlash;

public class PartFlashService
{
    private uint _diskSectorSize;
    public FileStream? Disk { get; private set; }

    public uint DiskSectorSize
    {
        get => _diskSectorSize;
        set
        {
            _diskSectorSize = value;
            IsVerified = false;
            VerifyDisk();
        }
    }

    public GuidPartitionEntry[] PartitionEntries { get; private set; } = [];

    public bool IsVerified { get; set; }

    public void OpenNewDisk(string disk)
    {
        if (Disk != null) throw new ArgumentException("Old dis is not closed.");

        Disk = File.Open(
            disk,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite);

        UpdateDiskSectorSize(disk);
        VerifyDisk();
    }

    public void Close()
    {
        if (Disk == null)
            return;

        Disk.Dispose();
        Disk = null;
        IsVerified = false;
    }

    private void UpdateDiskSectorSize(string name)
    {
        _diskSectorSize = OperatingSystem.IsWindows() ? GetDiskSectorSizeWindows(name) : GetDiskSectorSizePosix(name);
    }

    [SupportedOSPlatform("windows")]
    private uint GetDiskSectorSizeWindows(string name)
    {
        var deviceId = Regex.Match(name, @"\d+");
        var scope = new ManagementScope(@"\\localhost\ROOT\Microsoft\Windows\Storage");
        var query = new ObjectQuery("SELECT * FROM MSFT_PhysicalDisk");
        var searcher = new ManagementObjectSearcher(scope, query);
        var data = searcher.Get();
        foreach (var disk in data)
            if (disk["DeviceId"].ToString() == deviceId.Value)
                return Convert.ToUInt32(disk["LogicalSectorSize"]);

        // Fallback value
        return 4096;
    }

    private uint GetDiskSectorSizePosix(string name)
    {
        // TODO: Implement this
        return 4096;
    }

    private unsafe void VerifyDisk()
    {
        if (Disk == null)
            throw new ArgumentNullException(nameof(Disk));

        //Seek to LBA1
        Disk.Seek(DiskSectorSize, SeekOrigin.Begin);

        var buffer = stackalloc byte[(int)DiskSectorSize];
        var bufferSpan = new Span<byte>(buffer, (int)DiskSectorSize);

        ThrowHelper.ThrowArgumentExceptionIf(
            Disk.Read(bufferSpan) != DiskSectorSize,
            "Disk.Read(bufferSpan) != DiskSectorSize");

        var header = (GuidPartitionTableHeader*)buffer;
        VerifyPartitionHeader(header);
        VerifyPartitionEntryCrc(header);

        IsVerified = true;
    }

    private unsafe void VerifyPartitionHeader(GuidPartitionTableHeader* header)
    {
        ThrowHelper.ThrowArgumentExceptionIf(
            header->Signature != GuidPartitionTableHelper.CorrectSignature,
            "header->Signature != GuidPartitionTableHelper.CorrectSignature");

        ThrowHelper.ThrowArgumentExceptionIf(
            header->Revision != GuidPartitionTableHelper.SupportedRevision,
            "header->Revision != GuidPartitionTableHelper.SupportedRevision");

        ThrowHelper.ThrowArgumentExceptionIf(
            header->ReservedZero != 0,
            "header->ReservedZero != 0");

        ThrowHelper.ThrowArgumentExceptionIf(
            header->HeaderSize != Marshal.SizeOf<GuidPartitionTableHeader>(),
            "header->HeaderSize != sizeof(GuidPartitionTableHeader)");

        ThrowHelper.ThrowArgumentExceptionIf(
            header->PartitionEntrySize != Marshal.SizeOf<GuidPartitionEntry>(),
            "header->PartitionEntrySize != Marshal.SizeOf<GuidPartitionEntry>()");

        var crc = header->CrcValue;
        header->CrcValue = 0;

        var calculatedCrc = Crc32.Compute(new Span<byte>(header, (int)header->HeaderSize));
        ThrowHelper.ThrowArgumentExceptionIf(
            crc != calculatedCrc,
            "header->CrcValue != calculatedCrc");

        header->CrcValue = crc;
    }

    private unsafe void VerifyPartitionEntryCrc(GuidPartitionTableHeader* header)
    {
        var buffer = stackalloc byte[(int)DiskSectorSize];
        var entries = (GuidPartitionEntry*)buffer;
        var bufferSpan = new Span<byte>(buffer, (int)DiskSectorSize);

        var crc = 0u;

        var arr = new GuidPartitionEntry[header->PartitionEntriesCount];
        var entryIndex = 0;


        for (var i = header->PartitionEntriesStartLba; i < header->FirstUsableLba; i++)
        {
            Disk!.Seek((long)(DiskSectorSize * i), SeekOrigin.Begin);

            ThrowHelper.ThrowArgumentExceptionIf(
                Disk.Read(bufferSpan) != DiskSectorSize,
                "Disk.Read(bufferSpan) != DiskSectorSize");

            for (var j = 0; j < DiskSectorSize / header->PartitionEntrySize; j++, entryIndex++)
                arr[entryIndex] = entries[j];

            crc = Crc32.Compute(bufferSpan, crc);
        }

        ThrowHelper.ThrowArgumentExceptionIf(
            header->PartitionEntriesCrc != crc,
            "header->PartitionEntriesCrc != crc");

        PartitionEntries = arr;
    }
}
using HitRefresh.MobileSuit;

namespace WinPartFlash;

[SuitInfo("Flash")]
public class WinPartFlashClient(IIOHub io, PartFlashService flashService)
{
    [SuitAlias("open")]
    [SuitInfo("Open a new disk.")]
    public async Task OpenDisk(string disk)
    {
        if (flashService.Disk != null)
        {
            await io.WriteLineAsync("You have a disk opened, please close it first.");
            return;
        }

        if (OperatingSystem.IsWindows() && int.TryParse(disk, out var diskNumber))
            // Support "Open X" in windows
            disk = @$"\\.\PHYSICALDRIVE{diskNumber}";

        flashService.OpenNewDisk(disk);
    }

    [SuitAlias("print")]
    [SuitInfo("Print current disk info.")]
    public unsafe void PrintDiskInfo()
    {
        if (flashService.Disk == null)
        {
            io.WriteLine("Please open a disk first.");
            return;
        }

        io.WriteLine($"Disk sector size: {flashService.DiskSectorSize}, " +
                     $"Current sector size is correct (is ready to flash): {flashService.IsVerified}");
        var count = 0;
        var span = flashService.PartitionEntries.AsSpan();
        for (var index = 0; index < span.Length; index++)
        {
            var entry = span[index];
            if (entry.PartitionTypeGuid == Guid.Empty) continue;
            io.WriteLine($"Partition Number: {index}, " +
                         $"Partition Type: {entry.PartitionTypeGuid}, " +
                         $"Partition Name: {new Span<char>(entry.PartitionName, 36)}, " +
                         $"Partition Size: {Utils.BytesToHumanReadable(flashService.DiskSectorSize * (entry.EndLba - entry.StartLba + 1))}");
            count++;
        }

        io.WriteLine($"Total partition count: {count}");
    }

    [SuitAlias("close")]
    [SuitInfo("Close opened disk.")]
    public void Close()
    {
        flashService.Close();
    }

    [SuitAlias("setsec")]
    [SuitInfo("Force set sector size regardless the result read from system.")]
    public void SetSectorSize(uint size)
    {
        flashService.DiskSectorSize = size;
    }

    [SuitAlias("read")]
    [SuitInfo("Read a partition and save it to a file.")]
    public async Task Read(int partitionNumber, string fileName)
    {
        if (flashService.Disk == null || !flashService.IsVerified)
        {
            await io.WriteLineAsync("Please open a disk first.");
            return;
        }

        var partitionInfo = flashService.PartitionEntries.AsSpan()[partitionNumber];
        if (partitionInfo.StartLba == 0 || partitionInfo.EndLba == 0 || partitionInfo.StartLba >= partitionInfo.EndLba)
        {
            await io.WriteLineAsync("The partition argument does not seems right, please check again.");
            return;
        }

        var partitionSize = (partitionInfo.EndLba - partitionInfo.StartLba + 1) * flashService.DiskSectorSize;

        unsafe
        {
            io.WriteLine($"Partition Number: {partitionNumber}, " +
                         $"Partition Type: {partitionInfo.PartitionTypeGuid}, " +
                         $"Partition Name: {new Span<char>(partitionInfo.PartitionName, 36)}, " +
                         $"Partition Size: {Utils.BytesToHumanReadable(partitionSize)}");
        }

        await io.WriteLineAsync(
            $"About to save a {Utils.BytesToHumanReadable(partitionSize)} partition to {fileName}.");

        await using var file = File.Open(fileName, FileMode.CreateNew);

        //Seek to partition begin
        flashService.Disk.Seek((long)(partitionInfo.StartLba * flashService.DiskSectorSize), SeekOrigin.Begin);
        await Utils.CopyStream(flashService.Disk, file, partitionSize);

        await io.WriteLineAsync("Done.");

        await file.FlushAsync();
        file.Close();
    }

    [SuitAlias("write")]
    [SuitInfo("Write an image to a partition.")]
    public async Task Write(int partitionNumber, string fileName)
    {
        if (flashService.Disk == null || !flashService.IsVerified)
        {
            await io.WriteLineAsync("Please open a disk first.");
            return;
        }

        var partitionInfo = flashService.PartitionEntries.AsSpan()[partitionNumber];
        if (partitionInfo.StartLba == 0 || partitionInfo.EndLba == 0 || partitionInfo.StartLba >= partitionInfo.EndLba)
        {
            await io.WriteLineAsync("The partition argument does not seems right, please check again.");
            return;
        }

        var fileInfo = new FileInfo(fileName);
        if (!fileInfo.Exists)
        {
            await io.WriteLineAsync($"The file {fileInfo} does not exists.");
            return;
        }

        var partitionSize = (partitionInfo.EndLba - partitionInfo.StartLba + 1) * flashService.DiskSectorSize;

        if (partitionSize < (ulong)fileInfo.Length)
        {
            await io.WriteLineAsync("File is larger than partition " +
                                    $"({Utils.BytesToHumanReadable(fileInfo.Length)} vs " +
                                    $"{Utils.BytesToHumanReadable(partitionSize)}), aborting.");
            return;
        }

        unsafe
        {
            io.WriteLine($"Partition Number: {partitionNumber}, " +
                         $"Partition Type: {partitionInfo.PartitionTypeGuid}, " +
                         $"Partition Name: {new Span<char>(partitionInfo.PartitionName, 36)}, " +
                         $"Partition Size: {Utils.BytesToHumanReadable(partitionSize)}");
        }

        await io.WriteLineAsync(
            $"About to write {fileName} to a {Utils.BytesToHumanReadable(partitionSize)} partition.");

        var confirm = string.Empty;
        while (true)
        {
            if (confirm == "n")
                return;

            if (confirm == "y")
                break;

            confirm = (await io.ReadLineAsync("Are you sure? (y/n)"))?.ToLower();
        }

        await using var file = fileInfo.OpenRead();

        //Seek to partition begin
        flashService.Disk.Seek((long)(partitionInfo.StartLba * flashService.DiskSectorSize), SeekOrigin.Begin);
        await file.CopyToAsync(flashService.Disk);

        await io.WriteLineAsync("Done.");

        await file.FlushAsync();
        file.Close();
    }
}
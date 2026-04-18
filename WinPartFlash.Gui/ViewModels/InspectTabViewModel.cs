using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using WinPartFlash.Gui.Inspection;
using WinPartFlash.Gui.Logging;
using WinPartFlash.Gui.Resources;
using WinPartFlash.Gui.Utils;

namespace WinPartFlash.Gui.ViewModels;

public class InspectTabViewModel : ViewModelBase
{
    private readonly ILogSink _logSink;

    public InspectTabViewModel(ILogSink logSink)
    {
        _logSink = logSink;
        ScanGptCommand = ReactiveCommand.CreateFromTask<PartitionItemViewModel?>(ScanGptAsync);
        HexPeekCommand = ReactiveCommand.CreateFromTask<PartitionItemViewModel?>(HexPeekAsync);
    }

    public ObservableCollection<GuidPartitionEntryRow> GptEntries { get; } = new();

    public string GptHeaderSummary
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string GptStatus
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public long HexOffset
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int HexLength
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 512;

    public string HexDumpText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string HexStatus
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ReactiveCommand<PartitionItemViewModel?, Unit> ScanGptCommand { get; }
    public ReactiveCommand<PartitionItemViewModel?, Unit> HexPeekCommand { get; }

    private async Task ScanGptAsync(PartitionItemViewModel? partition)
    {
        GptEntries.Clear();
        GptHeaderSummary = string.Empty;
        GptStatus = string.Empty;

        if (partition is null)
        {
            GptStatus = Strings.InspectSelectPartition;
            return;
        }

        var deviceId = partition.Value.DiskDeviceId;
        if (string.IsNullOrEmpty(deviceId))
        {
            GptStatus = Strings.InspectGptNeedsDisk;
            return;
        }

        try
        {
            GuidPartitionTableSnapshot? snap = null;
            await Task.Run(() =>
            {
                using var stream = File.Open(deviceId, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                snap = GuidPartitionTableReader.TryRead(stream);
            });

            if (snap is null)
            {
                GptStatus = Strings.InspectGptNotFound;
                return;
            }

            var h = snap.Header;
            GptHeaderSummary = string.Format(
                Strings.InspectGptHeaderFormat,
                h.DiskGuid,
                snap.SectorSize,
                h.FirstUsableLba,
                h.LastUsableLba,
                h.PartitionEntriesCount,
                h.PartitionEntrySize);

            var index = 0;
            foreach (var entry in snap.Entries)
            {
                index++;
                if (entry.PartitionGuid == Guid.Empty) continue;
                GptEntries.Add(GuidPartitionEntryRow.From(entry, index, snap.SectorSize));
            }

            GptStatus = string.Format(Strings.InspectGptScanDone, GptEntries.Count);
            _logSink.Append(LogSeverity.Info,
                string.Format(Strings.InspectLogGptScan, deviceId, GptEntries.Count));
        }
        catch (Exception ex)
        {
            GptStatus = ex.Message;
            _logSink.Append(LogSeverity.Error, string.Format(Strings.LogOperationFailed, ex.Message));
        }
    }

    private async Task HexPeekAsync(PartitionItemViewModel? partition)
    {
        HexDumpText = string.Empty;
        HexStatus = string.Empty;

        if (partition is null)
        {
            HexStatus = Strings.InspectSelectPartition;
            return;
        }

        var length = HexLength;
        if (length <= 0)
        {
            HexStatus = Strings.InspectHexBadLength;
            return;
        }

        length = Math.Min(length, 65536);
        var offset = HexOffset;
        if (offset < 0)
        {
            HexStatus = Strings.InspectHexBadOffset;
            return;
        }

        try
        {
            byte[] buffer = new byte[length];
            int bytesRead = 0;
            await Task.Run(() =>
            {
                using var stream = partition.Value.OpenFileStream();
                if (stream.CanSeek)
                {
                    if (offset > stream.Length)
                        throw new IOException(Strings.InspectHexOffsetBeyondEnd);
                    stream.Seek(offset, SeekOrigin.Begin);
                }
                else if (offset > 0)
                {
                    throw new IOException(Strings.InspectHexStreamNotSeekable);
                }

                while (bytesRead < length)
                {
                    var n = stream.Read(buffer, bytesRead, length - bytesRead);
                    if (n <= 0) break;
                    bytesRead += n;
                }
            });

            HexDumpText = HexDump.Format(buffer.AsSpan(0, bytesRead), offset);
            HexStatus = string.Format(Strings.InspectHexDone,
                NumberHelper.BytesToHumanReadable((ulong)bytesRead), offset);
            _logSink.Append(LogSeverity.Info,
                string.Format(Strings.InspectLogHexPeek, partition.Value.Name, offset, bytesRead));
        }
        catch (Exception ex)
        {
            HexStatus = ex.Message;
            _logSink.Append(LogSeverity.Error, string.Format(Strings.LogOperationFailed, ex.Message));
        }
    }
}

public record GuidPartitionEntryRow(
    int Index,
    string Name,
    string TypeGuid,
    string PartitionGuid,
    string StartLba,
    string EndLba,
    string Size)
{
    public static unsafe GuidPartitionEntryRow From(
        GuidPartition.GuidPartitionEntry entry, int index, uint sectorSize)
    {
        string name;
        var nameChars = stackalloc char[36];
        var len = 0;
        for (var i = 0; i < 36; i++)
        {
            var c = entry.PartitionName[i];
            if (c == '\0') break;
            nameChars[i] = c;
            len++;
        }
        name = new(nameChars, 0, len);

        var bytes = (entry.EndLba - entry.StartLba + 1) * sectorSize;
        return new(
            index,
            name,
            entry.PartitionTypeGuid.ToString(),
            entry.PartitionGuid.ToString(),
            entry.StartLba.ToString(),
            entry.EndLba.ToString(),
            NumberHelper.BytesToHumanReadable(bytes));
    }
}

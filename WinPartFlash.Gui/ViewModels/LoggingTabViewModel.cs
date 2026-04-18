using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using ReactiveUI;
using WinPartFlash.Gui.Diagnostics;
using WinPartFlash.Gui.Logging;

namespace WinPartFlash.Gui.ViewModels;

public class LoggingTabViewModel : ViewModelBase
{
    private readonly ILogSink _logSink;

    public LoggingTabViewModel(ILogSink logSink, ISystemInfoProvider systemInfoProvider)
    {
        _logSink = logSink;
        SystemInfo = systemInfoProvider.GetSnapshot();
        CopyLogCommand = ReactiveCommand.CreateFromTask<Visual?>(CopyLog);
        ClearLogCommand = ReactiveCommand.Create(() => _logSink.Clear());
    }

    public SystemInfoSnapshot SystemInfo { get; }

    public ReadOnlyObservableCollection<LogEntry> LogEntries => _logSink.Entries;

    public ReactiveCommand<Visual?, Unit> CopyLogCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearLogCommand { get; }

    private async Task CopyLog(Visual? sender)
    {
        var clipboard = TopLevel.GetTopLevel(sender)?.Clipboard;
        if (clipboard == null) return;

        var builder = new StringBuilder();
        foreach (var entry in LogEntries.ToArray())
            builder.Append(entry.Timestamp.ToString("HH:mm:ss"))
                .Append(' ').Append('[').Append(entry.Severity).Append(']').Append(' ')
                .AppendLine(entry.Message);

        await clipboard.SetTextAsync(builder.ToString());
    }
}

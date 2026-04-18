using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace WinPartFlash.Gui.Logging;

public sealed class LogSink : ILogSink
{
    private const int Capacity = 500;

    private readonly ObservableCollection<LogEntry> _entries = new();

    public LogSink()
    {
        Entries = new(_entries);
    }

    public ReadOnlyObservableCollection<LogEntry> Entries { get; }

    public void Append(LogSeverity severity, string message)
    {
        var entry = new LogEntry(DateTime.Now, severity, message);
        if (Dispatcher.UIThread.CheckAccess())
            AppendOnUiThread(entry);
        else
            Dispatcher.UIThread.Post(() => AppendOnUiThread(entry));
    }

    public void Clear()
    {
        if (Dispatcher.UIThread.CheckAccess())
            _entries.Clear();
        else
            Dispatcher.UIThread.Post(() => _entries.Clear());
    }

    private void AppendOnUiThread(LogEntry entry)
    {
        _entries.Add(entry);
        while (_entries.Count > Capacity)
            _entries.RemoveAt(0);
    }
}

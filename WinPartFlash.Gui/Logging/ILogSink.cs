using System.Collections.ObjectModel;

namespace WinPartFlash.Gui.Logging;

public interface ILogSink
{
    ReadOnlyObservableCollection<LogEntry> Entries { get; }
    void Append(LogSeverity severity, string message);
    void Clear();
}

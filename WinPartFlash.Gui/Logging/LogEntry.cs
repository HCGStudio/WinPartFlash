using System;

namespace WinPartFlash.Gui.Logging;

public enum LogSeverity
{
    Info,
    Warning,
    Error
}

public sealed record LogEntry(DateTime Timestamp, LogSeverity Severity, string Message);

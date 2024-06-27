using System;

namespace WinPartFlash.Gui.Utils;

public static class NumberHelper
{
    private const double OneKb = 1024;

    private const double OneMb = OneKb * OneKb;

    private const double OneGb = OneMb * OneKb;

    private const double OneTb = OneGb * OneKb;

    public static string BytesToHumanReadable(double bytes)
    {
        return bytes switch
        {
            (< OneKb) => $"{bytes:F}B",
            (>= OneKb) and (< OneMb) => $"{bytes / OneKb:F}KB",
            (>= OneMb) and (< OneGb) => $"{bytes / OneMb:F}MB",
            (>= OneGb) and (< OneTb) => $"{bytes / OneGb:F}GB",
            (>= OneTb) => $"{bytes / OneTb:F}Tb",
            _ => throw new ArgumentOutOfRangeException(nameof(bytes), bytes, null)
        };
    }
}
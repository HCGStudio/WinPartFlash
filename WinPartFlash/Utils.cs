namespace WinPartFlash;

public static class Utils
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

    public static async ValueTask CopyStream(Stream source, Stream destination, ulong bytesToCopy)
    {
        var buffer = new byte[81920];
        var bytesRead = 0;
        ulong totalBytesCopied = 0;

        while (totalBytesCopied < bytesToCopy && (bytesRead = await source.ReadAsync(buffer)) > 0)
        {
            if (totalBytesCopied + (ulong)bytesRead > bytesToCopy) bytesRead = (int)(bytesToCopy - totalBytesCopied);

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalBytesCopied += (ulong)bytesRead;
        }
    }
}
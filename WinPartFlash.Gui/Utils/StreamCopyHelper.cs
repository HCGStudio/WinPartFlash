using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.Utils;

public static class StreamCopyHelper
{
    private const int BufferSize = 1 << 20;

    public static async ValueTask CopyAsync(
        Stream source,
        Stream destination,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[BufferSize];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken);
            if (read == 0) break;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            total += read;
            progress?.Report(total);
        }
    }
}

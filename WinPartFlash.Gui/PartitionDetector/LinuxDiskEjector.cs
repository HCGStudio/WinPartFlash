using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace WinPartFlash.Gui.PartitionDetector;

[SupportedOSPlatform("Linux")]
public sealed class LinuxDiskEjector : IDiskEjector
{
    public async Task EjectAsync(string diskDeviceId, CancellationToken cancellationToken = default)
    {
        if (await TryRunAsync("udisksctl", ["power-off", "-b", diskDeviceId], cancellationToken))
            return;
        if (await TryRunAsync("eject", [diskDeviceId], cancellationToken))
            return;
        throw new InvalidOperationException(
            $"Could not eject {diskDeviceId}: neither udisksctl nor eject succeeded.");
    }

    private static async Task<bool> TryRunAsync(string file, string[] args, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo(file)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

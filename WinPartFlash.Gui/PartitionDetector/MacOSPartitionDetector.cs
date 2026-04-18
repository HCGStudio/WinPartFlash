using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WinPartFlash.Gui.MacOS;

namespace WinPartFlash.Gui.PartitionDetector;

/// <summary>
/// macOS partition enumeration via <c>diskutil</c> (no root required) plus
/// per-partition raw I/O via <see cref="IPrivilegedDiskGateway"/>.  The
/// privileged helper is only invoked lazily, the first time the user actually
/// saves or flashes a partition.
/// </summary>
[SupportedOSPlatform("MacOS")]
public sealed partial class MacOSPartitionDetector : IPartitionDetector
{
    private readonly MacOSPrivilegedDiskGatewayFactory _gatewayFactory;
    private readonly ILogger<MacOSPartitionDetector> _logger;

    public MacOSPartitionDetector(
        MacOSPrivilegedDiskGatewayFactory gatewayFactory,
        ILogger<MacOSPartitionDetector> logger)
    {
        _gatewayFactory = gatewayFactory;
        _logger = logger;
    }

    public IList<PartitionResult> DetectPartitions()
    {
        // LoadPartitionsCommand fires on the Avalonia UI thread; awaiting
        // inline would deadlock the SynchronizationContext we'd need to
        // resume on.  Hop onto the thread pool with Task.Run before blocking.
        return Task.Run(async () =>
        {
            LogDetectStarted();
            var parts = await MacOSDiskUtil.ListAllPartitionsAsync();
            LogDetectFinished(parts.Count);
            return parts
                .Select(p => new PartitionResult(
                    p.DisplayName,
                    p.Length,
                    new Lazy<Stream>(() => OpenPartitionStream(p))))
                .ToList<PartitionResult>();
        }).GetAwaiter().GetResult();
    }

    private Stream OpenPartitionStream(MacOSDiskUtil.DiskUtilPartition p)
    {
        return Task.Run(async () =>
        {
            LogPartitionOpenRequested(p.Device, p.Offset, p.Length);
            await MacOSDiskUtil.UnmountDiskAsync(p.Device.Replace("/dev/r", "/dev/"), default);
            var gateway = await _gatewayFactory.GetAsync();
            return await gateway.OpenPartitionAsync(
                p.Device, p.Offset, p.Length, FileAccess.ReadWrite, default);
        }).GetAwaiter().GetResult();
    }

    [LoggerMessage(EventId = 300, Level = LogLevel.Information,
        Message = "Enumerating partitions via diskutil…")]
    private partial void LogDetectStarted();

    [LoggerMessage(EventId = 301, Level = LogLevel.Information,
        Message = "diskutil reported {Count} partitions.")]
    private partial void LogDetectFinished(int count);

    [LoggerMessage(EventId = 302, Level = LogLevel.Information,
        Message = "Opening partition stream for {Device} (offset={Offset}, length={Length}); this will request elevation.")]
    private partial void LogPartitionOpenRequested(string device, ulong offset, ulong length);
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using WinPartFlash.Gui.Compression;
using WinPartFlash.Gui.FileOpenHelper;
using WinPartFlash.Gui.Logging;
using WinPartFlash.Gui.MacOS;
using WinPartFlash.Gui.PartitionDetector;
using WinPartFlash.Gui.Resources;
using WinPartFlash.Gui.Utils;
using WinPartFlash.Gui.Views;

namespace WinPartFlash.Gui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private static readonly List<string> RawTrimEndings =
    [
        FileNameHelpers.FileExtensionGzip,
        FileNameHelpers.FileExtensionLz4,
        FileNameHelpers.FileExtensionZstandard
    ];

    private static readonly FilePickerFileType DiskImage = new(Strings.FileTypeNameDiskImages)
    {
        Patterns =
        [
            "*" + FileNameHelpers.FileExtensionImage,
            "*" + FileNameHelpers.FileExtensionImage + FileNameHelpers.FileExtensionGzip,
            "*" + FileNameHelpers.FileExtensionImage + FileNameHelpers.FileExtensionLz4,
            "*" + FileNameHelpers.FileExtensionImage + FileNameHelpers.FileExtensionZstandard
        ]
    };

    private readonly IPartitionDetector _partitionDetector;
    private readonly IDiskEjector _diskEjector;
    private readonly ICompressionStreamCopierFactory _streamCopierFactory;
    private readonly IFileOpenHelper _fileOpenHelper;
    private readonly ILogSink _logSink;

    private const long ProbeSampleBytes = 64L * 1024 * 1024;

    private readonly ObservableAsPropertyHelper<bool> _isMainWindowEnabled;
    private readonly ObservableAsPropertyHelper<bool> _isSaveButtonEnabled;
    private readonly ObservableAsPropertyHelper<bool> _isFlashButtonEnabled;
    private readonly ObservableAsPropertyHelper<bool> _isCompressionLevelEnabled;
    private readonly ObservableAsPropertyHelper<bool> _isZstdWorkersEnabled;
    private readonly ObservableAsPropertyHelper<bool> _isProbeCommandVisible;

    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _rateTimer;
    private long _bytesDoneRaw;
    private long _lastSampleBytes;
    private TimeSpan _lastSampleElapsed;
    private double _smoothedBytesPerSecond;

    public MainWindowViewModel(
        IPartitionDetector partitionDetector,
        IDiskEjector diskEjector,
        ICompressionStreamCopierFactory streamCopierFactory,
        IFileOpenHelper fileOpenHelper,
        ILogSink logSink,
        LoggingTabViewModel logging,
        InspectTabViewModel inspect)
    {
        _partitionDetector = partitionDetector;
        _diskEjector = diskEjector;
        _streamCopierFactory = streamCopierFactory;
        _fileOpenHelper = fileOpenHelper;
        _logSink = logSink;
        Logging = logging;
        Inspect = inspect;

        _rateTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
        _rateTimer.Tick += OnRateTimerTick;

        LoadPartitionsCommand = ReactiveCommand.Create(LoadPartitions);
        BrowseSaveFileCommand = ReactiveCommand.CreateFromTask<Button>(BrowseSaveFile);
        BrowseFlashFileCommand = ReactiveCommand.CreateFromTask<Button>(BrowseFlashFile);
        SavePartitionCommand = ReactiveCommand.CreateFromTask<Visual?>(SavePartition);
        FlashPartitionCommand = ReactiveCommand.CreateFromTask<Visual?>(FlashPartition);
        ProbeRatioCommand = ReactiveCommand.CreateFromTask(ProbeRatio);
        SwitchToSaveTabCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            SelectedTabIndex = 0;
            if (IsSaveButtonEnabled)
                await SavePartitionCommand.Execute(null);
        });
        SwitchToFlashTabCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            SelectedTabIndex = 1;
            if (IsFlashButtonEnabled)
                await FlashPartitionCommand.Execute(null);
        });
        SwitchToLoggingTabCommand = ReactiveCommand.Create(() => { SelectedTabIndex = 3; });
        SwitchToInspectTabCommand = ReactiveCommand.Create(() => { SelectedTabIndex = 2; });

        // Subscribe to update compression type by clicking button
        this
            .WhenAnyValue(vm => vm.IsSaveRawFileChecked)
            .Subscribe(enabled =>
            {
                if (enabled) CompressionType = CompressionType.Raw;
            });

        this
            .WhenAnyValue(vm => vm.IsSaveGzipFileChecked)
            .Subscribe(enabled =>
            {
                if (enabled) CompressionType = CompressionType.GzipCompress;
            });

        this
            .WhenAnyValue(vm => vm.IsSaveLz4FileChecked)
            .Subscribe(enabled =>
            {
                if (enabled) CompressionType = CompressionType.Lz4Compress;
            });

        this
            .WhenAnyValue(vm => vm.IsSaveZstandardFileChecked)
            .Subscribe(enabled =>
            {
                if (enabled) CompressionType = CompressionType.ZstandardCompress;
            });

        //Subscribe to update file name after compression type updated
        this
            .WhenAnyValue(vm => vm.CompressionType)
            .Subscribe(type =>
            {
                var ending = RawTrimEndings.FirstOrDefault(e => SavePartitionFileName.EndsWith(e));
                var rawFileName = ending == null ? SavePartitionFileName : SavePartitionFileName[..^ending.Length];
                switch (type)
                {
                    case CompressionType.Raw:
                        if (!string.IsNullOrWhiteSpace(rawFileName))
                            SavePartitionFileName = rawFileName;
                        break;
                    case CompressionType.GzipCompress:
                        if (!string.IsNullOrWhiteSpace(rawFileName))
                            SavePartitionFileName = rawFileName + FileNameHelpers.FileExtensionGzip;
                        break;
                    case CompressionType.Lz4Compress:
                        if (!string.IsNullOrWhiteSpace(rawFileName))
                            SavePartitionFileName = rawFileName + FileNameHelpers.FileExtensionLz4;
                        break;
                    case CompressionType.ZstandardCompress:
                        if (!string.IsNullOrWhiteSpace(rawFileName))
                            SavePartitionFileName = rawFileName + FileNameHelpers.FileExtensionZstandard;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });

        // Subscribe to update button status after file name updated
        this
            .WhenAnyValue(vm => vm.SavePartitionFileName)
            .Subscribe(name =>
            {
                if (name.EndsWith(FileNameHelpers.FileExtensionGzip))
                    IsSaveGzipFileChecked = true;
                else if (name.EndsWith(FileNameHelpers.FileExtensionLz4))
                    IsSaveLz4FileChecked = true;
                else if (name.EndsWith(FileNameHelpers.FileExtensionZstandard))
                    IsSaveZstandardFileChecked = true;
                else
                    IsSaveRawFileChecked = true;
            });

        // Reset level + workers to format defaults whenever the format changes.
        this
            .WhenAnyValue(vm => vm.CompressionType)
            .Subscribe(type =>
            {
                var (min, max, def) = CompressionLevelInfo.LevelRange(type);
                CompressionLevelMin = min;
                CompressionLevelMax = max;
                CompressionLevel = def;
                if (!CompressionLevelInfo.SupportsWorkers(type))
                    ZstdWorkers = 0;
                ProbeResultDisplay = string.Empty;
            });

        _isCompressionLevelEnabled = this
            .WhenAnyValue(vm => vm.CompressionType)
            .Select(CompressionLevelInfo.SupportsLevel)
            .ToProperty(this, vm => vm.IsCompressionLevelEnabled);

        _isZstdWorkersEnabled = this
            .WhenAnyValue(vm => vm.CompressionType)
            .Select(CompressionLevelInfo.SupportsWorkers)
            .ToProperty(this, vm => vm.IsZstdWorkersEnabled);

        _isProbeCommandVisible = this
            .WhenAnyValue(vm => vm.CompressionType)
            .Select(t => t != CompressionType.Raw)
            .ToProperty(this, vm => vm.IsProbeCommandVisible);

        _isSaveButtonEnabled = this
            .WhenAnyValue(vm => vm.SelectedPartition,
                vm => vm.SavePartitionFileName)
            .Select(tuple =>
            {
                var (selectedPartition, fileName) = tuple;
                return selectedPartition?.Value.Length != 0 && !string.IsNullOrWhiteSpace(fileName);
            })
            .ToProperty(this, vm => vm.IsSaveButtonEnabled);

        _isMainWindowEnabled = this
            .WhenAnyValue(vm => vm.IsBackgroundTaskRunning)
            .Select(b => !b)
            .ToProperty(this, vm => vm.IsMainWindowEnabled);

        _isFlashButtonEnabled = this
            .WhenAnyValue(vm => vm.SelectedPartition,
                vm => vm.FlashPartitionFileName)
            .Select(tuple =>
            {
                var (selectedPartition, fileName) = tuple;
                return selectedPartition?.Value.Length != 0 && _fileOpenHelper.IsSupported(fileName);
            })
            .ToProperty(this, vm => vm.IsFlashButtonEnabled);

        this
            .WhenAnyValue(vm => vm.WholeDiskMode, vm => vm.ProtectSystemDisk)
            .Skip(1)
            .Subscribe(_ => LoadPartitions());
    }

    public LoggingTabViewModel Logging { get; }
    public InspectTabViewModel Inspect { get; }

    public bool IsSaveZstandardFileChecked
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsSaveLz4FileChecked
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsFlashButtonEnabled => _isFlashButtonEnabled.Value;

    public int Progress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsBackgroundTaskRunning
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsProgressIndeterminate
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string ThroughputDisplay
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "—";

    public string EtaDisplay
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "—";

    public string ElapsedDisplay
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "—";

    public bool IsMainWindowEnabled => _isMainWindowEnabled.Value;
    public bool IsSaveButtonEnabled => _isSaveButtonEnabled.Value;

    public bool IsSaveRawFileChecked
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool IsSaveGzipFileChecked
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool VerifyAfterWrite
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool WholeDiskMode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ProtectSystemDisk
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool EjectAfterFlash
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string SavePartitionFileName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string FlashPartitionFileName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public PartitionItemViewModel? SelectedPartition
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public List<PartitionItemViewModel> PartitionItems
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = new();

    public ReactiveCommand<Unit, Unit> LoadPartitionsCommand { get; }
    public ReactiveCommand<Button, Unit> BrowseSaveFileCommand { get; }
    public ReactiveCommand<Button, Unit> BrowseFlashFileCommand { get; }
    public ReactiveCommand<Visual?, Unit> SavePartitionCommand { get; }
    public ReactiveCommand<Visual?, Unit> FlashPartitionCommand { get; }
    public ReactiveCommand<Unit, Unit> ProbeRatioCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToSaveTabCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToFlashTabCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToLoggingTabCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToInspectTabCommand { get; }

    public int SelectedTabIndex
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int CompressionLevel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int CompressionLevelMin
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int CompressionLevelMax
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = 1;

    public int ZstdWorkers
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsCompressionLevelEnabled => _isCompressionLevelEnabled.Value;
    public bool IsZstdWorkersEnabled => _isZstdWorkersEnabled.Value;
    public bool IsProbeCommandVisible => _isProbeCommandVisible.Value;

    public string ProbeResultDisplay
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;


    private long TotalBytes { get; set; }

    private CompressionType CompressionType
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private CompressionOptions BuildCompressionOptions()
    {
        var type = CompressionType;
        int? level = CompressionLevelInfo.SupportsLevel(type) ? CompressionLevel : null;
        int? workers = CompressionLevelInfo.SupportsWorkers(type) && ZstdWorkers > 0 ? ZstdWorkers : null;
        return new(level, workers);
    }

    private async Task ProbeRatio()
    {
        if (SelectedPartition is null || CompressionType == CompressionType.Raw)
        {
            ProbeResultDisplay = Strings.ProbeResultRaw;
            return;
        }

        var partition = SelectedPartition.Value;
        var sample = (long)Math.Min((ulong)ProbeSampleBytes, partition.Length);
        if (sample <= 0) return;

        try
        {
            IsBackgroundTaskRunning = true;
            await using var source = partition.OpenFileStream();
            var copier = _streamCopierFactory.GetCopier(CompressionType);
            var result = await CompressionRatioProbe.RunAsync(copier, source, BuildCompressionOptions(), sample);

            var sizeIn = NumberHelper.BytesToHumanReadable(result.BytesIn);
            var sizeOut = NumberHelper.BytesToHumanReadable(result.BytesOut);
            var elapsed = FormatDuration(result.Elapsed);
            ProbeResultDisplay = string.Format(Strings.ProbeResultFormat, sizeIn, sizeOut, result.Ratio, elapsed);
            _logSink.Append(LogSeverity.Info,
                string.Format(Strings.LogProbeCompleted, sizeIn, sizeOut, result.Ratio, elapsed));
        }
        catch (Exception ex)
        {
            _logSink.Append(LogSeverity.Error, string.Format(Strings.LogOperationFailed, ex.Message));
            ProbeResultDisplay = ex.Message;
        }
        finally
        {
            IsBackgroundTaskRunning = false;
        }
    }

    private static Window? GetVisualTopWindow(Visual? visual)
    {
        return TopLevel.GetTopLevel(visual) as Window;
    }

    private void LoadPartitions()
    {
        var options = new PartitionScanOptions(WholeDiskMode, ProtectSystemDisk);
        var partitions = _partitionDetector
            .DetectPartitions(options)
            .Select(p => new PartitionItemViewModel(p)).ToList();

        if (partitions.Count == 0)
            partitions.Add(new(new(
                Strings.ErrorNoPartition,
                0,
                () => Stream.Null)));

        PartitionItems = partitions;
    }

    private async Task BrowseSaveFile(Button sender)
    {
        var topLevel = TopLevel.GetTopLevel(sender);

        if (topLevel == null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new()
            {
                Title = Strings.SaveFileDialogTitle,
                DefaultExtension = ".img",
                FileTypeChoices = [DiskImage],
                ShowOverwritePrompt = true
            });

        if (file != null)
            SavePartitionFileName = file.TryGetLocalPath() ?? string.Empty;
    }

    private async Task BrowseFlashFile(Button sender)
    {
        var topLevel = TopLevel.GetTopLevel(sender);

        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new()
            {
                FileTypeFilter = [DiskImage],
                AllowMultiple = false
            });

        if (files.Count > 0)
            FlashPartitionFileName = files[0].TryGetLocalPath() ?? string.Empty;
    }

    private async Task SavePartition(Visual? sender)
    {
        var partition = SelectedPartition!.Value;
        var destination = SavePartitionFileName;
        StartOperation((long)partition.Length);
        _logSink.Append(LogSeverity.Info, string.Format(
            Strings.LogSaveStarted,
            partition.Name,
            NumberHelper.BytesToHumanReadable(partition.Length),
            destination));

        try
        {
            await using var rawSource = partition.OpenFileStream();
            await using var hashingSource = new HashingStream(rawSource, HashAlgorithmName.SHA256, leaveOpen: true);
            await using var output = File.Open(destination, FileMode.Create);

            var copier = _streamCopierFactory.GetCopier(CompressionType);
            var progress = new Progress<long>(bytes => Interlocked.Exchange(ref _bytesDoneRaw, bytes));

            await copier.CopyToStreamAsync(hashingSource, output, BuildCompressionOptions(), progress);

            var hash = hashingSource.GetHashAndReset();
            await ChecksumSidecar.WriteAsync(destination, hash);

            Progress = 100;
            _logSink.Append(LogSeverity.Info, string.Format(
                Strings.LogChecksumSidecarWritten,
                ChecksumSidecar.SidecarPathFor(Path.GetFileName(destination)),
                Convert.ToHexString(hash).ToLowerInvariant()));
            _logSink.Append(LogSeverity.Info, string.Format(
                Strings.LogSaveCompleted,
                destination,
                FormatDuration(_stopwatch.Elapsed)));
        }
        catch (Exception ex)
        {
            _logSink.Append(LogSeverity.Error, string.Format(Strings.LogOperationFailed, ex.Message));
            await ShowErrorDialogAsync(sender, ex);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task FlashPartition(Visual? sender)
    {
        var topWindow = GetVisualTopWindow(sender);
        var partition = SelectedPartition!.Value;
        var source = FlashPartitionFileName;

        try
        {
            var confirmText = string.Format(
                Strings.ConfirmFlashText,
                NumberHelper.BytesToHumanReadable(partition.Length),
                partition.Name);

            var confirmed = await MessageDialog.ConfirmAsync(topWindow, Strings.ConfirmDialogTitle, confirmText);
            if (!confirmed)
                return;

            StartOperation((long)partition.Length);
            _logSink.Append(LogSeverity.Info, string.Format(
                Strings.LogFlashStarted,
                source,
                partition.Name,
                NumberHelper.BytesToHumanReadable(partition.Length)));

            var expectedHash = ChecksumSidecar.TryReadHash(source);
            if (expectedHash != null)
                _logSink.Append(LogSeverity.Info, string.Format(
                    Strings.LogChecksumSidecarFound,
                    ChecksumSidecar.SidecarPathFor(Path.GetFileName(source))));

            var tuple = await _fileOpenHelper.OpenRead(source);
            await using var inputStream = tuple.Item1;
            var streamCopier = _streamCopierFactory.GetCopier(tuple.Item2);
            var progress = new Progress<long>(bytes => Interlocked.Exchange(ref _bytesDoneRaw, bytes));

            long bytesWritten;
            byte[] writtenHash;
            await using (var rawTarget = partition.OpenFileStream())
            await using (var hashingTarget = new HashingStream(rawTarget, HashAlgorithmName.SHA256, leaveOpen: true))
            {
                await streamCopier.CopyToStreamAsync(inputStream, hashingTarget, default, progress);
                await hashingTarget.FlushAsync();
                bytesWritten = hashingTarget.BytesProcessed;
                writtenHash = hashingTarget.GetHashAndReset();
            }

            if (expectedHash != null && !CryptographicOperations.FixedTimeEquals(expectedHash, writtenHash))
                throw new InvalidDataException(string.Format(
                    Strings.ErrorChecksumMismatch,
                    Convert.ToHexString(expectedHash).ToLowerInvariant(),
                    Convert.ToHexString(writtenHash).ToLowerInvariant()));

            if (expectedHash != null)
                _logSink.Append(LogSeverity.Info, string.Format(
                    Strings.LogChecksumVerified,
                    Convert.ToHexString(writtenHash).ToLowerInvariant()));

            if (VerifyAfterWrite)
            {
                _logSink.Append(LogSeverity.Info, Strings.LogVerifyStarted);
                var readbackHash = await ReadbackHashAsync(partition, bytesWritten);
                if (!CryptographicOperations.FixedTimeEquals(readbackHash, writtenHash))
                    throw new InvalidDataException(string.Format(
                        Strings.ErrorVerifyMismatch,
                        Convert.ToHexString(writtenHash).ToLowerInvariant(),
                        Convert.ToHexString(readbackHash).ToLowerInvariant()));

                _logSink.Append(LogSeverity.Info, string.Format(
                    Strings.LogVerifyCompleted,
                    Convert.ToHexString(readbackHash).ToLowerInvariant()));
            }

            Progress = 100;
            _logSink.Append(LogSeverity.Info, string.Format(
                Strings.LogFlashCompleted,
                partition.Name,
                FormatDuration(_stopwatch.Elapsed)));

            if (EjectAfterFlash && !string.IsNullOrEmpty(partition.DiskDeviceId))
            {
                try
                {
                    await _diskEjector.EjectAsync(partition.DiskDeviceId);
                    _logSink.Append(LogSeverity.Info,
                        string.Format(Strings.LogEjectCompleted, partition.DiskDeviceId));
                }
                catch (Exception ejectEx)
                {
                    _logSink.Append(LogSeverity.Error,
                        string.Format(Strings.LogEjectFailed, partition.DiskDeviceId, ejectEx.Message));
                }
            }
        }
        catch (Exception ex)
        {
            _logSink.Append(LogSeverity.Error, string.Format(Strings.LogOperationFailed, ex.Message));
            await ShowErrorDialogAsync(sender, ex);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task<byte[]> ReadbackHashAsync(PartitionResult partition, long bytesToHash)
    {
        await using var stream = partition.OpenFileStream();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1 << 20];
        long remaining = bytesToHash;
        long cumulative = 0;
        Interlocked.Exchange(ref _bytesDoneRaw, 0);
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead));
            if (read == 0)
                throw new EndOfStreamException();
            hash.AppendData(buffer, 0, read);
            remaining -= read;
            cumulative += read;
            Interlocked.Exchange(ref _bytesDoneRaw, cumulative);
        }
        return hash.GetHashAndReset();
    }

    private async Task ShowErrorDialogAsync(Visual? sender, Exception ex)
    {
        var topWindow = GetVisualTopWindow(sender);
        var (title, body) = MapPrivilegedException(ex);
        await MessageDialog.ShowErrorAsync(topWindow, title, body);
    }

    private void StartOperation(long totalBytes)
    {
        IsBackgroundTaskRunning = true;
        Progress = 0;
        TotalBytes = totalBytes;
        IsProgressIndeterminate = totalBytes <= 0;
        Interlocked.Exchange(ref _bytesDoneRaw, 0);
        _lastSampleBytes = 0;
        _lastSampleElapsed = TimeSpan.Zero;
        _smoothedBytesPerSecond = 0;
        ThroughputDisplay = "—";
        EtaDisplay = "—";
        ElapsedDisplay = FormatDuration(TimeSpan.Zero);
        _stopwatch.Restart();
        _rateTimer.Start();
    }

    private void EndOperation()
    {
        _rateTimer.Stop();
        _stopwatch.Stop();
        ElapsedDisplay = FormatDuration(_stopwatch.Elapsed);
        IsBackgroundTaskRunning = false;
        IsProgressIndeterminate = false;
    }

    private void OnRateTimerTick(object? sender, EventArgs e)
    {
        var bytes = Interlocked.Read(ref _bytesDoneRaw);
        var elapsed = _stopwatch.Elapsed;

        if (TotalBytes > 0)
            Progress = (int)Math.Min(100, bytes * 100L / TotalBytes);
        ElapsedDisplay = FormatDuration(elapsed);

        var deltaTime = elapsed - _lastSampleElapsed;
        if (deltaTime > TimeSpan.Zero)
        {
            var instantaneous = (bytes - _lastSampleBytes) / deltaTime.TotalSeconds;
            _smoothedBytesPerSecond = _smoothedBytesPerSecond == 0
                ? instantaneous
                : _smoothedBytesPerSecond * 0.6 + instantaneous * 0.4;

            if (_smoothedBytesPerSecond > 0)
                ThroughputDisplay = NumberHelper.BytesToHumanReadable(_smoothedBytesPerSecond) + "/s";

            if (TotalBytes > 0 && _smoothedBytesPerSecond > 0 && bytes < TotalBytes)
            {
                var remainingSec = (TotalBytes - bytes) / _smoothedBytesPerSecond;
                EtaDisplay = FormatDuration(TimeSpan.FromSeconds(remainingSec));
            }
            else if (bytes >= TotalBytes && TotalBytes > 0)
            {
                EtaDisplay = FormatDuration(TimeSpan.Zero);
            }
        }

        _lastSampleBytes = bytes;
        _lastSampleElapsed = elapsed;
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalSeconds < 0) span = TimeSpan.Zero;
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours:D1}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes:D2}:{span.Seconds:D2}";
    }

    /// <summary>
    /// Translates the macOS privileged-disk gateway's typed exceptions into
    /// user-friendly localized titles + bodies.  Falls back to the underlying
    /// exception's message + stack trace for anything we don't recognise.
    /// </summary>
    private static (string Title, string Body) MapPrivilegedException(Exception ex)
    {
        return ex switch
        {
            PrivilegedAuthorizationCancelledException
                => (Strings.ConfirmDialogTitle, Strings.ErrorPrivilegedAuthorizationCancelled),
            PrivilegedHelperUnavailableException
                => (Strings.ConfirmDialogTitle, Strings.ErrorPrivilegedHelperUnavailable),
            PrivilegedAuthorizationFailedException
                => (Strings.ConfirmDialogTitle,
                    string.Format(Strings.ErrorPrivilegedAuthorizationFailed, ex.Message)),
            DeviceBusyException
                => (Strings.ConfirmDialogTitle, Strings.ErrorDeviceBusy),
            _ => (ex.Message, ex.StackTrace ?? string.Empty)
        };
    }
}

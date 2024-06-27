using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using WinPartFlash.Gui.Compression;
using WinPartFlash.Gui.FileOpenHelper;
using WinPartFlash.Gui.PartitionDetector;
using WinPartFlash.Gui.Resources;
using WinPartFlash.Gui.Utils;

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
    private readonly ICompressionStreamCopierFactory _streamCopierFactory;
    private readonly IFileOpenHelper _fileOpenHelper;

    private readonly ObservableAsPropertyHelper<bool> _isMainWindowEnabled;
    private readonly ObservableAsPropertyHelper<bool> _isSaveButtonEnabled;
    private readonly ObservableAsPropertyHelper<bool> _isFlashButtonEnabled;

    private CompressionType _compressionType;

    private bool _isSaveGzipFileChecked;
    private bool _isSaveRawFileChecked = true;
    private bool _isSaveZstandardFileChecked;
    private bool _isSaveLz4FileChecked;
    private bool _isBackgroundTaskRunning;

    private int _progress;

    private List<PartitionItemViewModel> _partitionItems = new();

    private string _savePartitionFileName = string.Empty;
    private string _flashPartitionFileName = string.Empty;

    private PartitionItemViewModel? _selectedPartition;

    public MainWindowViewModel(
        IPartitionDetector partitionDetector,
        ICompressionStreamCopierFactory streamCopierFactory,
        IFileOpenHelper fileOpenHelper)
    {
        _partitionDetector = partitionDetector;
        _streamCopierFactory = streamCopierFactory;
        _fileOpenHelper = fileOpenHelper;
        LoadPartitionsCommand = ReactiveCommand.Create(LoadPartitions);
        BrowseSaveFileCommand = ReactiveCommand.CreateFromTask<Button>(BrowseSaveFile);
        BrowseFlashFileCommand = ReactiveCommand.CreateFromTask<Button>(BrowseFlashFile);
        SavePartitionCommand = ReactiveCommand.CreateFromTask<Visual?>(SavePartition);
        FlashPartitionCommand = ReactiveCommand.CreateFromTask<Visual?>(FlashPartition);

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
    }

    public bool IsSaveZstandardFileChecked
    {
        get => _isSaveZstandardFileChecked;
        set => this.RaiseAndSetIfChanged(ref _isSaveZstandardFileChecked, value);
    }

    public bool IsSaveLz4FileChecked
    {
        get => _isSaveLz4FileChecked;
        set => this.RaiseAndSetIfChanged(ref _isSaveLz4FileChecked, value);
    }

    public bool IsFlashButtonEnabled => _isFlashButtonEnabled.Value;

    public int Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    public bool IsBackgroundTaskRunning
    {
        get => _isBackgroundTaskRunning;
        set => this.RaiseAndSetIfChanged(ref _isBackgroundTaskRunning, value);
    }

    public bool IsMainWindowEnabled => _isMainWindowEnabled.Value;
    public bool IsSaveButtonEnabled => _isSaveButtonEnabled.Value;

    public bool IsSaveRawFileChecked
    {
        get => _isSaveRawFileChecked;
        set => this.RaiseAndSetIfChanged(ref _isSaveRawFileChecked, value);
    }

    public bool IsSaveGzipFileChecked
    {
        get => _isSaveGzipFileChecked;
        set => this.RaiseAndSetIfChanged(ref _isSaveGzipFileChecked, value);
    }

    public string SavePartitionFileName
    {
        get => _savePartitionFileName;
        set => this.RaiseAndSetIfChanged(ref _savePartitionFileName, value);
    }

    public string FlashPartitionFileName
    {
        get => _flashPartitionFileName;
        set => this.RaiseAndSetIfChanged(ref _flashPartitionFileName, value);
    }

    public PartitionItemViewModel? SelectedPartition
    {
        get => _selectedPartition;
        set => this.RaiseAndSetIfChanged(ref _selectedPartition, value);
    }

    public List<PartitionItemViewModel> PartitionItems
    {
        get => _partitionItems;
        set => this.RaiseAndSetIfChanged(ref _partitionItems, value);
    }

    public ReactiveCommand<Unit, Unit> LoadPartitionsCommand { get; }
    public ReactiveCommand<Button, Unit> BrowseSaveFileCommand { get; }
    public ReactiveCommand<Button, Unit> BrowseFlashFileCommand { get; }
    public ReactiveCommand<Visual?, Unit> SavePartitionCommand { get; }
    public ReactiveCommand<Visual?, Unit> FlashPartitionCommand { get; }

    private CompressionType CompressionType
    {
        get => _compressionType;
        set => this.RaiseAndSetIfChanged(ref _compressionType, value);
    }

    private static Window? GetVisualTopWindow(Visual? visual)
    {
        return TopLevel.GetTopLevel(visual) as Window;
    }

    private void LoadPartitions()
    {
        var partitions = _partitionDetector
            .DetectPartitions()
            .Select(p => new PartitionItemViewModel(p)).ToList();

        if (partitions.Count == 0)
            partitions.Add(new PartitionItemViewModel(new PartitionResult(
                Strings.ErrorNoPartition,
                0,
                new Lazy<Stream>())));

        PartitionItems = partitions;
    }

    private async Task BrowseSaveFile(Button sender)
    {
        var topLevel = TopLevel.GetTopLevel(sender);

        if (topLevel == null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save File",
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
            new FilePickerOpenOptions
            {
                FileTypeFilter = [DiskImage],
                AllowMultiple = false
            });

        if (files.Count > 0)
            FlashPartitionFileName = files[0].TryGetLocalPath() ?? string.Empty;
    }

    private async Task SavePartition(Visual? sender)
    {
        IsBackgroundTaskRunning = true;
        Progress = 0;

        try
        {
            await using var source = SelectedPartition!.Value.OpenFileStream.Value;
            await using var destination = File.Open(SavePartitionFileName, FileMode.Create);

            var copier = _streamCopierFactory.GetCopier(CompressionType);

            await copier.CopyToStreamAsync(source, destination);

            Progress = 100;
        }
        catch (Exception ex)
        {
            var topWindow = GetVisualTopWindow(sender);
            var box = MessageBoxManager.GetMessageBoxStandard(
                ex.Message,
                ex.StackTrace,
                ButtonEnum.Ok,
                Icon.Error);

            if (topWindow == null)
                await box.ShowAsync();
            else
                await box.ShowWindowDialogAsync(topWindow);
        }
        finally
        {
            IsBackgroundTaskRunning = false;
        }
    }

    private async Task FlashPartition(Visual? sender)
    {
        IsBackgroundTaskRunning = true;
        Progress = 0;
        var topWindow = GetVisualTopWindow(sender);

        try
        {
            var selectedPartition = SelectedPartition!.Value;
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Confirm",
                string.Format(
                    Strings.ConfirmFlashText,
                    NumberHelper.BytesToHumanReadable(selectedPartition.Length),
                    selectedPartition.Name),
                ButtonEnum.YesNo);

            var result = topWindow == null ? await box.ShowAsync() : await box.ShowWindowDialogAsync(topWindow);
            if (result != ButtonResult.Yes)
                return;

            var tuple = await _fileOpenHelper.OpenRead(FlashPartitionFileName);
            await using var inputStream = tuple.Item1;
            var streamCopier = _streamCopierFactory.GetCopier(tuple.Item2);


            await streamCopier.CopyToStreamAsync(inputStream, selectedPartition.OpenFileStream.Value);
        }
        catch (Exception ex)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(
                ex.Message,
                ex.StackTrace,
                ButtonEnum.Ok,
                Icon.Error);

            if (topWindow == null)
                await box.ShowAsync();
            else
                await box.ShowWindowDialogAsync(topWindow);
        }
        finally
        {
            IsBackgroundTaskRunning = false;
        }
    }
}
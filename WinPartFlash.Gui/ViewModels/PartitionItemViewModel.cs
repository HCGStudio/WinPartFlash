using WinPartFlash.Gui.PartitionDetector;

namespace WinPartFlash.Gui.ViewModels;

public class PartitionItemViewModel(PartitionResult value) : ViewModelBase
{
    public PartitionResult Value { get; } = value;
}
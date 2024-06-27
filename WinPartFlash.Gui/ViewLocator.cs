using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WinPartFlash.Gui.ViewModels;
using WinPartFlash.Gui.Views;

namespace WinPartFlash.Gui;

public class ViewLocator : IDataTemplate
{
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }

    public Control? Build(object? data)
    {
        if (data == null)
            return null;

        if (data is PartitionItemViewModel partitionItemViewModel)
            return new PartitionItemView(partitionItemViewModel);

        return null;
    }
}
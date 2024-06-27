using System;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using WinPartFlash.Gui.ViewModels;

namespace WinPartFlash.Gui.Views;

public partial class PartitionItemView : ReactiveUserControl<PartitionItemViewModel>
{
    public PartitionItemView(PartitionItemViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private void PartitionItemViewInitialized(object? sender, EventArgs e)
    {
        if (Parent is ComboBoxItem comboBoxItem) comboBoxItem.IsEnabled = ViewModel!.Value.Length != 0;
    }
}
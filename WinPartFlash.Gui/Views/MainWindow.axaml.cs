using Avalonia.ReactiveUI;
using WinPartFlash.Gui.ViewModels;

namespace WinPartFlash.Gui.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    //public MainWindowViewModel ViewModel { get; set; }
}
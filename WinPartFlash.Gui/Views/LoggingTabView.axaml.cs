using ReactiveUI.Avalonia;
using WinPartFlash.Gui.ViewModels;

namespace WinPartFlash.Gui.Views;

public partial class LoggingTabView : ReactiveUserControl<LoggingTabViewModel>
{
    public LoggingTabView(LoggingTabViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}

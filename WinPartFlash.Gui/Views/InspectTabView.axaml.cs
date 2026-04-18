using ReactiveUI.Avalonia;
using WinPartFlash.Gui.ViewModels;

namespace WinPartFlash.Gui.Views;

public partial class InspectTabView : ReactiveUserControl<InspectTabViewModel>
{
    public InspectTabView(InspectTabViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}

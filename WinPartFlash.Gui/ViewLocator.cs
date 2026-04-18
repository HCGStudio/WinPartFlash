using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Extensions.DependencyInjection;
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
        return data switch
        {
            PartitionItemViewModel vm => new PartitionItemView(vm),
            LoggingTabViewModel => App.ServiceProvider.GetRequiredService<LoggingTabView>(),
            InspectTabViewModel => App.ServiceProvider.GetRequiredService<InspectTabView>(),
            _ => null
        };
    }
}

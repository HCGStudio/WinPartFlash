using System;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using WinPartFlash.Gui.ViewModels;

namespace WinPartFlash.Gui.Utils;

public static class CommonExtensions
{
    public static IServiceCollection AddViewsAndViewModels(this IServiceCollection serviceCollection)
    {
        var allTypes = Assembly.GetCallingAssembly().GetTypes();

        var views = allTypes.Where(t => t.IsSubclassOf(typeof(Control))).ToArray();
        var viewModels = allTypes.Where(t => t.IsSubclassOf(typeof(ViewModelBase)));

        foreach (var viewModel in viewModels)
        {
            var typeOfIViewFor = typeof(IViewFor<>).MakeGenericType(viewModel);
            serviceCollection.AddScoped(viewModel);

            //Find view
            var viewType = views.FirstOrDefault(t => t.IsAssignableTo(typeOfIViewFor));
            serviceCollection.AddScoped(typeOfIViewFor, viewType ?? throw new InvalidOperationException());
            serviceCollection.AddScoped(viewType);
        }

        return serviceCollection;
    }
}
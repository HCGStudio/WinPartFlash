using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WinPartFlash.Gui.Compression;
using WinPartFlash.Gui.FileOpenHelper;
using WinPartFlash.Gui.PartitionDetector;
using WinPartFlash.Gui.Resources;
using WinPartFlash.Gui.Utils;
using WinPartFlash.Gui.Views;

namespace WinPartFlash.Gui;

public class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Why this is not set automatically?
        Strings.Culture = CultureInfo.CurrentUICulture;
        var services = new ServiceCollection();
        services.AddPartitionDetector();
        services.AddCompressionStreamCopier();
        services.AddViewsAndViewModels();
        services.AddFileOpenHelper();

        ServiceProvider = services.BuildServiceProvider();

        using var scope = ServiceProvider.CreateScope();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();

        base.OnFrameworkInitializationCompleted();
    }
}
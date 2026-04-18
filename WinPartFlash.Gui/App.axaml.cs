using System;
using System.Globalization;
using System.Reactive;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using WinPartFlash.Gui.Compression;
using WinPartFlash.Gui.Diagnostics;
using WinPartFlash.Gui.FileOpenHelper;
using WinPartFlash.Gui.Logging;
using WinPartFlash.Gui.MacOS;
using WinPartFlash.Gui.PartitionDetector;
using WinPartFlash.Gui.Resources;
using WinPartFlash.Gui.Utils;
using WinPartFlash.Gui.Views;

namespace WinPartFlash.Gui;

public class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    public string AppName => Strings.AppName;

    public ReactiveCommand<Unit, Unit> ShowAboutCommand { get; }

    public App()
    {
        ShowAboutCommand = ReactiveCommand.CreateFromTask(ShowAbout);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = this;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Why this is not set automatically?
        Strings.Culture = CultureInfo.CurrentUICulture;
        var services = new ServiceCollection();
        services.AddLogging(builder => builder
            .AddSimpleConsole(opt =>
            {
                opt.SingleLine = true;
                opt.TimestampFormat = "HH:mm:ss.fff ";
            })
            .SetMinimumLevel(LogLevel.Information));
        services.AddMacOSPrivileges();
        services.AddPartitionDetector();
        services.AddCompressionStreamCopier();
        services.AddLogSink();
        services.AddDiagnostics();
        services.AddViewsAndViewModels();
        services.AddFileOpenHelper();

        ServiceProvider = services.BuildServiceProvider();

        using var scope = ServiceProvider.CreateScope();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task ShowAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "—";
        var body = string.Format(Strings.AboutDialogFormat, Strings.AppName, version, Strings.AppSubtitle);
        await MessageDialog.ShowInfoAsync(null, Strings.AboutDialogTitle, body);
    }
}
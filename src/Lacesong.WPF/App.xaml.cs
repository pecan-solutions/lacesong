using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Services;
using Lacesong.WPF.ViewModels;
using Lacesong.WPF.Services;
using System.Windows;
using Karambolo.Extensions.Logging.File;
using Wpf.Ui;

namespace Lacesong.WPF;

/// <summary>
/// main application entry point
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // show main window
        var mainWindow = new MainWindow(
            _serviceProvider.GetRequiredService<MainViewModel>(),
            _serviceProvider);
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // core services
        services.AddSingleton<IGameDetector, GameDetector>();
        services.AddSingleton<IBepInExManager, BepInExManager>();
        services.AddSingleton<IModManager, ModManager>();
        services.AddSingleton<IBackupManager, BackupManager>();
        services.AddSingleton<IModIndexService, ModIndexService>();
        services.AddSingleton<IVerificationService, VerificationService>();
        services.AddSingleton<IDependencyResolver, DependencyResolver>();
        services.AddSingleton<IModUpdateService, ModUpdateService>();
        services.AddSingleton<IConflictDetectionService, ConflictDetectionService>();
        services.AddSingleton<ICompatibilityService, CompatibilityService>();
        services.AddSingleton<IModConfigService, ModConfigService>();

        // wpf services
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();

        // view models
        services.AddTransient<MainViewModel>();
        services.AddTransient<GameDetectionViewModel>();
        services.AddTransient<BepInExInstallViewModel>();
        services.AddTransient<ModCatalogViewModel>();
        services.AddTransient<SettingsViewModel>();

        // views
        services.AddTransient<MainWindow>();
        services.AddTransient<Views.HomeView>();
        services.AddTransient<Views.GameDetectionView>();
        services.AddTransient<Views.BepInExInstallView>();
        services.AddTransient<Views.ModCatalogView>();
        services.AddTransient<Views.SettingsView>();

        // logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddFile(o => { o.Files = new[] { new LogFileOptions { Path = "logs/lacesong-{Date}.log" } }; });
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

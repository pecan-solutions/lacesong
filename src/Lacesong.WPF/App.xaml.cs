using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Services;
using Lacesong.WPF.ViewModels;
using Lacesong.WPF.Services;
using Lacesong.WPF.Views;
using System.Windows;
using Karambolo.Extensions.Logging.File;

namespace Lacesong.WPF;

/// <summary>
/// main application entry point
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Services not initialized");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
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
        services.AddSingleton<IGameLauncher, GameLauncher>();
        services.AddSingleton<Lacesong.Core.Services.ThunderstoreApiService>();
        services.AddSingleton<IThunderstoreApiService>(sp => sp.GetRequiredService<Lacesong.Core.Services.ThunderstoreApiService>());

        // wpf services
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();

        // view models
        services.AddTransient<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<GameDetectionViewModel>();
        services.AddTransient<GameNotSelectedViewModel>();
        services.AddTransient<BepInExInstallViewModel>();
        services.AddTransient<ModCatalogViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ModSettingsViewModelFactory>();
        services.AddTransient<BrowseModsViewModel>();

        // views
        services.AddTransient<MainWindow>();
        services.AddTransient<Views.HomeView>();
        services.AddTransient<Views.GameDetectionView>();
        services.AddTransient<Views.BepInExInstallView>();
        services.AddTransient<Views.ModCatalogView>();
        services.AddTransient<Views.SettingsView>();
        services.AddTransient<Views.BrowseModsView>();

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

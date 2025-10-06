using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Karambolo.Extensions.Logging.File;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Services;
using Lacesong.Avalonia.Services;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Lacesong.Avalonia;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("services not initialized");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // core services
        services.AddSingleton<IGameDetector, GameDetector>();
        services.AddSingleton<IBepInExManager, BepInExManager>();
        services.AddSingleton<IModManager, ModManager>();
        services.AddSingleton<IBackupManager, BackupManager>();
        services.AddSingleton<IModIndexService, ThunderstoreModIndexService>();
        services.AddSingleton<ThunderstoreService>();
        services.AddSingleton<IVerificationService, VerificationService>();
        services.AddSingleton<IDependencyResolver, DependencyResolver>();
        services.AddSingleton<IModUpdateService, ModUpdateService>();
        services.AddSingleton<IConflictDetectionService, ConflictDetectionService>();
        services.AddSingleton<ICompatibilityService, CompatibilityService>();
        services.AddSingleton<IModConfigService, ModConfigService>();
        services.AddSingleton<IGameLauncher, GameLauncher>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // avalonia services
        services.AddSingleton<ILoggingService, Services.LoggingService>();
        services.AddSingleton<IUpdateService, Services.UpdateService>();
        services.AddSingleton<IDialogService, Services.DialogService>();
        services.AddSingleton<IContentDialogService, Services.ContentDialogService>();
        services.AddSingleton<INavigationService, Services.NavigationService>();
        services.AddSingleton<ISnackbarService, Services.SnackbarService>(); // parameterless now
        services.AddSingleton<IGameStateService, Services.GameStateService>();

        // memory cache for thunderstore api caching
        services.AddMemoryCache();

        // logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddFile(o =>
            {
                o.RootPath = AppContext.BaseDirectory;
                o.Files = new[] { new LogFileOptions { Path = "logs/lacesong-{Date}.log" } };
            });
        });

        // view models
        services.AddSingleton<ViewModels.MainViewModel>();
        services.AddTransient<ViewModels.HomeViewModel>();
        services.AddTransient<ViewModels.GameDetectionViewModel>();
        services.AddTransient<ViewModels.BepInExInstallViewModel>();
        services.AddTransient<ViewModels.InstalledModsViewModel>();
        services.AddTransient<ViewModels.SettingsViewModel>();
        services.AddTransient<ViewModels.BrowseModsViewModel>();

        // views
        services.AddSingleton<MainWindow>();
        services.AddTransient<Views.HomeView>();
        services.AddTransient<Views.GameDetectionView>();
        services.AddTransient<Views.BepInExInstallView>();
        services.AddTransient<Views.InstalledModsView>();
        services.AddTransient<Views.SettingsView>();
        services.AddTransient<Views.BrowseModsView>();
    }

}
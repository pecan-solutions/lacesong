using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Karambolo.Extensions.Logging.File;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Services;
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
        services.AddSingleton<IModIndexService, ModIndexService>();
        services.AddSingleton<IVerificationService, VerificationService>();
        services.AddSingleton<IDependencyResolver, DependencyResolver>();
        services.AddSingleton<IModUpdateService, ModUpdateService>();
        services.AddSingleton<IConflictDetectionService, ConflictDetectionService>();
        services.AddSingleton<ICompatibilityService, CompatibilityService>();
        services.AddSingleton<IModConfigService, ModConfigService>();
        services.AddSingleton<IGameLauncher, GameLauncher>();
        services.AddSingleton<IThunderstoreApiService, ThunderstoreApiService>();

        // logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddFile(o => { o.Files = new[] { new LogFileOptions { Path = "logs/lacesong-{Date}.log" } }; });
        });

        // views
        services.AddTransient<MainWindow>();
    }

    public override void OnExited()
    {
        _serviceProvider?.Dispose();
        base.OnExited();
    }
}
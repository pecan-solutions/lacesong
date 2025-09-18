using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.WPF.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;
using System;

namespace Lacesong.WPF.ViewModels;

/// <summary>
/// main view model for the application
/// </summary>
public partial class MainViewModel : BaseViewModel
{
    private readonly IGameDetector _gameDetector;
    private readonly IDialogService _dialogService;
    private readonly ILoggingService _loggingService;
    private readonly IUpdateService _updateService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private GameInstallation? _currentGame;

    [ObservableProperty]
    private bool _isGameDetected;

    [ObservableProperty]
    private string _currentView = "Home";

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _updateMessage = string.Empty;

    [ObservableProperty]
    private BaseViewModel? _currentViewModel;

    public MainViewModel(
        ILogger<MainViewModel> logger,
        IGameDetector gameDetector,
        IDialogService dialogService,
        ILoggingService loggingService,
        IUpdateService updateService,
        IServiceProvider serviceProvider) : base(logger)
    {
        _gameDetector = gameDetector;
        _dialogService = dialogService;
        _loggingService = loggingService;
        _updateService = updateService;
        _serviceProvider = serviceProvider;

        // initialize with game detection
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await ExecuteAsync(async () =>
        {
            // check for updates
            await CheckForUpdatesAsync();
            
            // detect game installation automatically
            await DetectGameAsync();
            
            // if game is detected, go directly to mod management
            if (IsGameDetected && CurrentGame != null)
            {
                Navigate(typeof(HomeViewModel));
                SetStatus($"Ready to manage mods for {CurrentGame.Name}");
            }
            else
            {
                // navigate to game detection if no game is found
                Navigate(typeof(GameDetectionViewModel));
                SetStatus("Please detect or select your game installation");
            }
        }, "Initializing Lacesong...");
    }

    [RelayCommand]
    private async Task DetectGameAsync()
    {
        await ExecuteAsync(async () =>
        {
            SetStatus("Detecting game installation...");
            
            var game = await _gameDetector.DetectGameInstall();
            if (game != null)
            {
                CurrentGame = game;
                IsGameDetected = true;
                SetStatus($"Detected {game.Name} at {game.InstallPath}");
                
                // navigate to home if game is detected
                Navigate(typeof(HomeViewModel));
            }
            else
            {
                IsGameDetected = false;
                SetStatus("No game installation detected. Please select a game directory manually.");
                // stay on game detection screen
                Navigate(typeof(GameDetectionViewModel));
            }
        });
    }

    [RelayCommand]
    private async Task SelectGameDirectoryAsync()
    {
        var selectedPath = await _dialogService.ShowFolderDialogAsync("Select Game Directory");
        if (!string.IsNullOrEmpty(selectedPath))
        {
            await ExecuteAsync(async () =>
            {
                SetStatus("Detecting game from selected directory...");
                
                var game = await _gameDetector.DetectGameInstall(selectedPath);
                if (game != null)
                {
                    CurrentGame = game;
                    IsGameDetected = true;
                    SetStatus($"Detected {game.Name} at {game.InstallPath}");
                    Navigate(typeof(HomeViewModel));
                }
                else
                {
                    SetStatus("No valid game installation found in selected directory.", true);
                }
            });
        }
    }

    [RelayCommand]
    private void Navigate(Type viewModelType)
    {
        CurrentViewModel = (BaseViewModel?)_serviceProvider.GetService(viewModelType);
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        await ExecuteAsync(async () =>
        {
            SetStatus("Checking for updates...");
            
            var updateInfo = await _updateService.CheckForUpdatesAsync();
            if (updateInfo.IsUpdateAvailable)
            {
                IsUpdateAvailable = true;
                UpdateMessage = $"Update available: {updateInfo.LatestVersion}";
                SetStatus($"Update available: {updateInfo.LatestVersion}");
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateMessage = "You are running the latest version";
                SetStatus("You are running the latest version");
            }
        });
    }

    [RelayCommand]
    private async Task DownloadUpdateAsync()
    {
        if (!IsUpdateAvailable) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Download Update",
            "A new version is available. Would you like to download it now?");
            
        if (result)
        {
            await ExecuteAsync(async () =>
            {
                SetStatus("Downloading update...");
                await _updateService.DownloadUpdateAsync();
                SetStatus("Update downloaded successfully. Please restart the application.");
            });
        }
    }

    [RelayCommand]
    private async Task LaunchModdedAsync()
    {
        if (CurrentGame == null) return;
        await ExecuteAsync(async () =>
        {
            var launcher = (IGameLauncher)_serviceProvider.GetRequiredService(typeof(IGameLauncher));
            var result = await launcher.LaunchModded(CurrentGame);
            SetStatus(result.Success ? "modded game launched" : result.Error ?? "failed to launch");
        });
    }

    [RelayCommand]
    private async Task LaunchVanillaAsync()
    {
        if (CurrentGame == null) return;
        await ExecuteAsync(async () =>
        {
            var launcher = (IGameLauncher)_serviceProvider.GetRequiredService(typeof(IGameLauncher));
            var result = await launcher.LaunchVanilla(CurrentGame);
            SetStatus(result.Success ? "vanilla game launched" : result.Error ?? "failed to launch");
        });
    }

    [RelayCommand]
    private void OpenLogs()
    {
        _loggingService.OpenLogsFolder();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        Navigate(typeof(SettingsViewModel));
    }

    [RelayCommand]
    private void ExitApplication()
    {
        Application.Current.Shutdown();
    }
}

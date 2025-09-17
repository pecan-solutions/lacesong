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
    private readonly INavigationService _navigationService;

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

    public MainViewModel(
        ILogger<MainViewModel> logger,
        IGameDetector gameDetector,
        IDialogService dialogService,
        ILoggingService loggingService,
        IUpdateService updateService,
        INavigationService navigationService) : base(logger)
    {
        _gameDetector = gameDetector;
        _dialogService = dialogService;
        _loggingService = loggingService;
        _updateService = updateService;
        _navigationService = navigationService;

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
                _navigationService.Navigate(typeof(Views.HomeView));
                SetStatus($"Ready to manage mods for {CurrentGame.Name}");
            }
            else
            {
                // navigate to game detection if no game is found
                _navigationService.Navigate(typeof(Views.GameDetectionView));
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
                _navigationService.Navigate(typeof(Views.HomeView));
            }
            else
            {
                IsGameDetected = false;
                SetStatus("No game installation detected. Please select a game directory manually.");
                // stay on game detection screen
                _navigationService.Navigate(typeof(Views.GameDetectionView));
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
                    _navigationService.Navigate(typeof(Views.HomeView));
                }
                else
                {
                    SetStatus("No valid game installation found in selected directory.", true);
                }
            });
        }
    }

    [RelayCommand]
    private void NavigateToView(string viewName)
    {
        // This command is now obsolete with NavigationView
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
    private void OpenLogs()
    {
        _loggingService.OpenLogsFolder();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _navigationService.Navigate(typeof(Views.SettingsView));
    }

    [RelayCommand]
    private void ExitApplication()
    {
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private Task InstallModFromFileAsync()
    {
        if (!IsGameDetected || CurrentGame == null)
        {
            SetStatus("Please detect your game installation first.", true);
            return Task.CompletedTask;
        }

        return ExecuteAsync(async () =>
        {
            var filePath = await _dialogService.ShowOpenFileDialogAsync("Select Mod File", "ZIP files (*.zip)|*.zip|All files (*.*)|*.*");
            if (!string.IsNullOrEmpty(filePath))
            {
                SetStatus("Installing mod from file...");
                
                // this would need to be implemented with the mod manager
                SetStatus($"Would install mod from: {filePath}");
            }
        });
    }

    [RelayCommand]
    private Task InstallModFromUrlAsync()
    {
        if (!IsGameDetected || CurrentGame == null)
        {
            SetStatus("Please detect your game installation first.", true);
            return Task.CompletedTask;
        }

        return ExecuteAsync(async () =>
        {
            var url = await _dialogService.ShowInputDialogAsync("Install Mod from URL", "Enter the URL of the mod to install:");
            if (!string.IsNullOrEmpty(url))
            {
                SetStatus("Installing mod from URL...");
                
                // this would need to be implemented with the mod manager
                SetStatus($"Would install mod from: {url}");
            }
        });
    }
}

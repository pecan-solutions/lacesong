using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Avalonia.Services;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Threading;
using Avalonia.Threading;

namespace Lacesong.Avalonia.ViewModels;

public partial class HomeViewModel : BaseViewModel, IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly IGameStateService _gameStateService;
    private readonly IGameDetector _gameDetector;
    private readonly IModManager _modManager;
    private readonly IDialogService _dialogService;
    private readonly IGameLauncher _gameLauncher;
    private readonly IBepInExManager _bepInExManager;
    private readonly ISnackbarService _snackbarService;

    // process monitoring
    private CancellationTokenSource? _processMonitoringCts;
    private Task? _processMonitoringTask;
    private bool _disposed;

    [ObservableProperty]
    private string _gameStatusText = string.Empty;

    public enum LaunchMode { None, Modded, Vanilla }

    [ObservableProperty]
    private LaunchMode _activeMode = LaunchMode.None;

    [ObservableProperty]
    private bool _isGameRunning;

    public string ModdedButtonText => ActiveMode == LaunchMode.Modded && IsGameRunning ? "Stop Game" : "Launch Modded";
    public string VanillaButtonText => ActiveMode == LaunchMode.Vanilla && IsGameRunning ? "Stop Game" : "Launch Vanilla";

    partial void OnActiveModeChanged(LaunchMode oldValue, LaunchMode newValue)
    {
        OnPropertyChanged(nameof(ModdedButtonText));
        OnPropertyChanged(nameof(VanillaButtonText));
        DetectCommandCanExecChanged();
    }

    partial void OnIsGameRunningChanged(bool oldValue, bool newValue)
    {
        OnPropertyChanged(nameof(ModdedButtonText));
        OnPropertyChanged(nameof(VanillaButtonText));
        DetectCommandCanExecChanged();
    }

    private void DetectCommandCanExecChanged()
    {
        LaunchModdedCommand.NotifyCanExecuteChanged();
        LaunchVanillaCommand.NotifyCanExecuteChanged();
        InstallModFromFileCommand.NotifyCanExecuteChanged();
        InstallModFromUrlCommand.NotifyCanExecuteChanged();
    }

    public GameInstallation CurrentGame => _gameStateService.CurrentGame;
    public bool IsGameDetected => _gameStateService.IsGameDetected;
    public string SelectGameButtonText => IsGameDetected ? "Change Game" : "Select Game Manually";

    public HomeViewModel(
        ILogger<HomeViewModel> logger, 
        INavigationService navigationService, 
        IGameStateService gameStateService,
        IGameDetector gameDetector,
        IModManager modManager,
        IDialogService dialogService,
        IGameLauncher gameLauncher,
        IBepInExManager bepInExManager,
        ISnackbarService snackbarService) : base(logger)
    {
        _navigationService = navigationService;
        _gameStateService = gameStateService;
        _gameDetector = gameDetector;
        _modManager = modManager;
        _dialogService = dialogService;
        _gameLauncher = gameLauncher;
        _bepInExManager = bepInExManager;
        _snackbarService = snackbarService;

        _gameStateService.GameStateChanged += OnGameStateChanged;
        _gameStateService.OnBepInExUpdateAvailable += OnBepInExUpdateAvailable;
        
        _ = InitialGameDetectionAsync();
    }

    private async Task InitialGameDetectionAsync()
    {
        if (IsGameDetected)
        {
            // game already detected from previous session, just update the display
            UpdateGameStatusText();
            return;
        }
        
        // attempt auto-detection with a timeout
        GameStatusText = "Attempting to automatically detect game...";
        
        var detectionTask = DetectGameCommand.ExecuteAsync(null);
        var timeoutTask = Task.Delay(2000);
        
        await Task.WhenAny(detectionTask, timeoutTask);
        
        // after timeout, if still not detected, show "Game not detected"
        if (!IsGameDetected)
        {
            GameStatusText = "Game not detected";
        }
    }
    
    private void OnGameStateChanged()
    {
        OnPropertyChanged(nameof(CurrentGame));
        OnPropertyChanged(nameof(IsGameDetected));
        OnPropertyChanged(nameof(SelectGameButtonText));
        
        UpdateGameStatusText();
    }

    private void UpdateGameStatusText()
    {
        if (IsGameDetected)
        {
            GameStatusText = $"Game: {CurrentGame.Name}\nPath: {CurrentGame.InstallPath}";
        }
        else
        {
            GameStatusText = "Game not detected";
        }
    }

    private void StartProcessMonitoring()
    {
        // stop any existing monitoring
        StopProcessMonitoring();

        if (!IsGameDetected || !IsGameRunning)
            return;

        _processMonitoringCts = new CancellationTokenSource();
        _processMonitoringTask = Task.Run(async () =>
        {
            try
            {
                while (!_processMonitoringCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, _processMonitoringCts.Token); // check every second

                    // check if game is still running
                    if (!_gameLauncher.IsRunning(CurrentGame))
                    {
                        // game process has exited, update ui state
                        // marshal to ui thread using dispatcher
                        Dispatcher.UIThread.Post(() =>
                        {
                            IsGameRunning = false;
                            ActiveMode = LaunchMode.None;
                            SetStatus("Game exited.");
                        });
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected when cancellation is requested
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in process monitoring");
            }
        }, _processMonitoringCts.Token);
    }

    private void StopProcessMonitoring()
    {
        _processMonitoringCts?.Cancel();
        _processMonitoringTask?.Wait(1000); // wait up to 1 second for graceful shutdown
        _processMonitoringCts?.Dispose();
        _processMonitoringCts = null;
        _processMonitoringTask = null;
    }

    [RelayCommand]
    private void GoToBrowseMods() => _navigationService.NavigateTo<BrowseModsViewModel>();

    private bool CanStartGame()
    {
        return IsGameDetected && !IsGameRunning;
    }

    private bool CanStopGame()
    {
        return IsGameDetected && IsGameRunning;
    }

    private bool CanExecuteLaunchModded()
    {
        if (!IsGameDetected) return false;
        
        // can start modded if no game is running
        if (!IsGameRunning) return true;
        
        // can stop if modded game is currently running
        return ActiveMode == LaunchMode.Modded;
    }

    private bool CanExecuteLaunchVanilla()
    {
        if (!IsGameDetected) return false;
        
        // can start vanilla if no game is running
        if (!IsGameRunning) return true;
        
        // can stop if vanilla game is currently running
        return ActiveMode == LaunchMode.Vanilla;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteLaunchModded))]
    private async Task LaunchModded()
    {
        if (!IsGameRunning)
        {
            await ExecuteAsync(async () =>
            {
                var result = await _gameLauncher.LaunchModded(CurrentGame);
                if (result.Success)
                {
                    ActiveMode = LaunchMode.Modded;
                    IsGameRunning = true;
                    StartProcessMonitoring(); // start monitoring after successful launch
                }
                SetStatus(result.Success ? "Launched modded game." : $"Failed to launch: {result.Message}");
            }, "Launching modded game...");
        }
        else if (ActiveMode == LaunchMode.Modded)
        {
            await ExecuteAsync(async () =>
            {
                var result = await _gameLauncher.Stop(CurrentGame ?? throw new InvalidOperationException("No game selected"));
                if (result.Success)
                {
                    IsGameRunning = false;
                    ActiveMode = LaunchMode.None;
                    StopProcessMonitoring(); // stop monitoring when manually stopped
                }
                SetStatus(result.Success ? "Game stopped." : result.Message);
            }, "Stopping game...");
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteLaunchVanilla))]
    private async Task LaunchVanilla()
    {
        Console.WriteLine($"[DEBUG] LaunchVanilla called - IsGameRunning: {IsGameRunning}");
        Console.WriteLine($"[DEBUG] CurrentGame: {(CurrentGame != null ? $"Path: {CurrentGame.InstallPath}, Executable: {CurrentGame.Executable}" : "null")}");
        
        if (!IsGameRunning)
        {
            await ExecuteAsync(async () =>
            {
                Console.WriteLine($"[DEBUG] About to call _gameLauncher.LaunchVanilla with game at: {CurrentGame?.InstallPath}");
                var result = await _gameLauncher.LaunchVanilla(CurrentGame ?? throw new InvalidOperationException("No game selected"));
                Console.WriteLine($"[DEBUG] LaunchVanilla result - Success: {result.Success}, Message: {result.Message}, Error: {result.Error}");
                
                if (result.Success)
                {
                    Console.WriteLine($"[DEBUG] Launch successful, setting ActiveMode to Vanilla and IsGameRunning to true");
                    ActiveMode = LaunchMode.Vanilla;
                    IsGameRunning = true;
                    StartProcessMonitoring(); // start monitoring after successful launch
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Launch failed with message: {result.Message}");
                }
                SetStatus(result.Success ? "Launched vanilla game." : $"Failed to launch: {result.Message}");
            }, "Launching vanilla game...");
        }
        else if (ActiveMode == LaunchMode.Vanilla)
        {
            Console.WriteLine($"[DEBUG] Game is running in vanilla mode, stopping game");
            await ExecuteAsync(async () =>
            {
                var result = await _gameLauncher.Stop(CurrentGame ?? throw new InvalidOperationException("No game selected"));
                if (result.Success)
                {
                    IsGameRunning = false;
                    ActiveMode = LaunchMode.None;
                    StopProcessMonitoring(); // stop monitoring when manually stopped
                }
                SetStatus(result.Success ? "Game stopped." : result.Message);
            }, "Stopping game...");
        }
        else
        {
            Console.WriteLine($"[DEBUG] Game is running but not in vanilla mode, skipping launch");
        }
    }

    [RelayCommand]
    private async Task DetectGame()
    {
        await ExecuteAsync(async () =>
        {
            var game = await _gameDetector.DetectGameInstall();
            if (game != null)
            {
                _gameStateService.SetCurrentGame(game);
                SetStatus($"Game detected: {game.Name}");
            }
            else
            {
                UpdateGameStatusText();
            }
        }, "Detecting game...");
    }

    [RelayCommand]
    private async Task SelectGameDirectory()
    {
        await ExecuteAsync(async () =>
        {
            var path = await _dialogService.ShowFolderDialogAsync("Select Game Directory");
            if (!string.IsNullOrEmpty(path))
            {
                var game = await _gameDetector.DetectGameInstall(path);
                if (game != null)
                {
                    _gameStateService.SetCurrentGame(game);
                    SetStatus($"Game set to: {game.Name}");
                }
                else
                {
                    SetStatus("Selected directory is not a valid game installation.");
                    UpdateGameStatusText();
                }
            }
        }, "Selecting game directory...");
    }

    [RelayCommand(CanExecute = nameof(CanStartGame))]
    private async Task InstallModFromFile()
    {
        await ExecuteAsync(async () =>
        {
            if (!IsGameDetected)
            {
                SetStatus("Please detect a game first.");
                return;
            }

            var filePath = await _dialogService.ShowOpenFileDialogAsync("Select Mod ZIP", "zip");
            if (string.IsNullOrEmpty(filePath))
                return;

            var result = await _modManager.InstallModFromZip(filePath, CurrentGame);
            SetStatus(result.Success ? "Mod installed successfully." : $"Install failed: {result.Message}");
        }, "Installing mod from file...");
    }

    [RelayCommand(CanExecute = nameof(CanStartGame))]
    private async Task InstallModFromUrl()
    {
        await ExecuteAsync(async () =>
        {
            if (!IsGameDetected)
            {
                SetStatus("Please detect a game first.");
                return;
            }

            var url = await _dialogService.ShowInputDialogAsync("Install Mod from URL", "Enter the URL to a mod zip file:");
            if (string.IsNullOrEmpty(url))
                return;

            var result = await _modManager.InstallModFromZip(url, CurrentGame);
            SetStatus(result.Success ? "Mod installed successfully." : $"Install failed: {result.Message}");
        }, "Installing mod from URL...");
    }

    private async void OnBepInExUpdateAvailable(BepInExUpdate update)
    {
        // show notification about available BepInEx update
        _snackbarService.Show(
            "BepInEx Update Available", 
            $"BepInEx {update.LatestVersion} is available (current: {update.CurrentVersion})", 
            "Information", 
            "🔄", 
            TimeSpan.FromSeconds(10)
        );

        // automatically trigger the update with progress reporting
        await ExecuteAsync(async () =>
        {
            var progress = new Progress<double>(progressValue =>
            {
                var percentage = (int)(progressValue * 100);
                SetStatus($"Updating BepInEx... {percentage}%");
            });

            var result = await _bepInExManager.UpdateBepInEx(CurrentGame, null, progress);
            if (result.Success)
            {
                SetStatus($"BepInEx updated successfully: {result.Message}");
                _snackbarService.Show("BepInEx Updated", "BepInEx has been updated successfully", "Success", "✅");
            }
            else
            {
                SetStatus($"BepInEx update failed: {result.Message}");
                _snackbarService.Show("Update Failed", result.Message, "Error", "❌");
            }
        }, "Updating BepInEx...");
    }

    [RelayCommand]
    private async Task UpdateBepInEx()
    {
        if (!IsGameDetected)
        {
            SetStatus("Please detect a game first.");
            return;
        }

        await ExecuteAsync(async () =>
        {
            var progress = new Progress<double>(progressValue =>
            {
                var percentage = (int)(progressValue * 100);
                SetStatus($"Updating BepInEx... {percentage}%");
            });

            var result = await _bepInExManager.UpdateBepInEx(CurrentGame, null, progress);
            if (result.Success)
            {
                SetStatus($"BepInEx updated successfully: {result.Message}");
                _snackbarService.Show("BepInEx Updated", "BepInEx has been updated successfully", "Success", "✅");
            }
            else
            {
                SetStatus($"BepInEx update failed: {result.Message}");
                _snackbarService.Show("Update Failed", result.Message, "Error", "❌");
            }
        }, "Updating BepInEx...");
    }

    [RelayCommand]
    private async Task CleanupBepInExBackups()
    {
        if (!IsGameDetected)
        {
            SetStatus("Please detect a game first.");
            return;
        }

        await ExecuteAsync(async () =>
        {
            var result = await _bepInExManager.CleanupBepInExBackups(CurrentGame);
            if (result.Success)
            {
                SetStatus($"Backup cleanup completed: {result.Message}");
                _snackbarService.Show("Backup Cleanup", "Old backups have been cleaned up successfully", "Success", "🗑️");
            }
            else
            {
                SetStatus($"Backup cleanup failed: {result.Message}");
                _snackbarService.Show("Cleanup Failed", result.Message, "Error", "❌");
            }
        }, "Cleaning up old backups...");
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _gameStateService.GameStateChanged -= OnGameStateChanged;
        _gameStateService.OnBepInExUpdateAvailable -= OnBepInExUpdateAvailable;
        StopProcessMonitoring();
        _disposed = true;
    }
}


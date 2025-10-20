using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Threading;
using Avalonia.Threading;
using Lacesong.Avalonia.Services;

namespace Lacesong.Avalonia.ViewModels;

public partial class BottomBarViewModel : BaseViewModel, IDisposable, IAsyncDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IGameLauncher _gameLauncher;
    private readonly ISettingsService _settingsService;

    private CancellationTokenSource? _processMonitoringCts;
    private Task? _processMonitoringTask;
    private bool _disposed;

    public enum LaunchMode { Modded, Vanilla }

    [ObservableProperty]
    private LaunchMode _selectedLaunchMode;

    [ObservableProperty]
    private bool _isGameRunning;

    [ObservableProperty]
    private LaunchMode _activeLaunchMode;

    public string LaunchButtonText
    {
        get
        {
            if (IsGameRunning)
            {
                return "Stop Game";
            }
            return _selectedLaunchMode == LaunchMode.Modded ? "Launch Modded" : "Launch Vanilla";
        }
    }

    public BottomBarViewModel(
        ILogger<BottomBarViewModel> logger,
        IGameStateService gameStateService,
        IGameLauncher gameLauncher,
        ISettingsService settingsService) : base(logger)
    {
        _gameStateService = gameStateService;
        _gameLauncher = gameLauncher;
        _settingsService = settingsService;

        _gameStateService.GameStateChanged += OnGameStateChanged;

        // Load persisted launch mode
        _settingsService.LoadSettings();
        if (Enum.TryParse<LaunchMode>(_settingsService.CurrentSettings.PreferredLaunchMode, out var mode))
        {
            _selectedLaunchMode = mode;
        }
        else
        {
            _selectedLaunchMode = LaunchMode.Modded; // Default
        }
        
        OnPropertyChanged(nameof(LaunchButtonText));
    }

    partial void OnSelectedLaunchModeChanged(LaunchMode value)
    {
        OnPropertyChanged(nameof(LaunchButtonText));
        _settingsService.CurrentSettings.PreferredLaunchMode = value.ToString();
        _settingsService.SaveSettings();
        LaunchCommand.NotifyCanExecuteChanged();
    }
    
    partial void OnIsGameRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(LaunchButtonText));
        LaunchCommand.NotifyCanExecuteChanged();
    }

    private void OnGameStateChanged()
    {
        LaunchCommand.NotifyCanExecuteChanged();
        
        // start process monitoring when game is detected
        if (_gameStateService.IsGameDetected)
        {
            StartProcessMonitoring();
        }
        else
        {
            StopProcessMonitoring();
        }
    }

    private void StartProcessMonitoring()
    {
        StopProcessMonitoring();

        if (!_gameStateService.IsGameDetected)
            return;

        _processMonitoringCts = new CancellationTokenSource();
        _processMonitoringTask = Task.Run(async () =>
        {
            try
            {
                while (!_processMonitoringCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, _processMonitoringCts.Token);

                    var isCurrentlyRunning = _gameLauncher.IsRunning(_gameStateService.CurrentGame);
                    
                    // update IsGameRunning if the state has changed
                    if (IsGameRunning != isCurrentlyRunning)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            IsGameRunning = isCurrentlyRunning;
                            if (!isCurrentlyRunning)
                            {
                                ActiveLaunchMode = LaunchMode.Modded; // reset to default when game stops
                            }
                        });
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in process monitoring");
            }
        }, _processMonitoringCts.Token);
    }

    private async Task StopProcessMonitoringAsync()
    {
        _processMonitoringCts?.Cancel();
        if (_processMonitoringTask != null)
        {
            try
            {
                await _processMonitoringTask.WaitAsync(TimeSpan.FromMilliseconds(1000));
            }
            catch (TimeoutException)
            {
                // task didn't complete within timeout, which is acceptable
            }
        }
        _processMonitoringCts?.Dispose();
        _processMonitoringCts = null;
        _processMonitoringTask = null;
    }

    private void StopProcessMonitoring()
    {
        _processMonitoringCts?.Cancel();
        _processMonitoringCts?.Dispose();
        _processMonitoringCts = null;
        _processMonitoringTask = null;
    }

    private bool CanLaunchGame()
    {
        return _gameStateService.IsGameDetected;
    }

    [RelayCommand(CanExecute = nameof(CanLaunchGame))]
    private async Task Launch()
    {
        if (IsGameRunning)
        {
            await ExecuteAsync(async () =>
            {
                var result = await _gameLauncher.Stop(_gameStateService.CurrentGame);
                if (result.Success)
                {
                    IsGameRunning = false;
                    StopProcessMonitoring();
                }
                SetStatus(result.Success ? "Game stopped." : result.Message);
            }, "Stopping game...");
        }
        else
        {
            await ExecuteAsync(async () =>
            {
                OperationResult result;
                if (SelectedLaunchMode == LaunchMode.Vanilla)
                {
                    result = await _gameLauncher.LaunchVanilla(_gameStateService.CurrentGame);
                }
                else
                {
                    result = await _gameLauncher.LaunchModded(_gameStateService.CurrentGame);
                }

                if (result.Success)
                {
                    ActiveLaunchMode = SelectedLaunchMode;
                    IsGameRunning = true;
                    StartProcessMonitoring();
                }
                SetStatus(result.Success ? $"Launched {SelectedLaunchMode.ToString().ToLower()} game." : $"Failed to launch: {result.Message}");
            }, $"Launching {SelectedLaunchMode.ToString().ToLower()} game...");
        }
    }
    
    [RelayCommand]
    private void SetLaunchMode(string mode)
    {
        if (Enum.TryParse<LaunchMode>(mode, true, out var launchMode))
        {
            SelectedLaunchMode = launchMode;
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _gameStateService.GameStateChanged -= OnGameStateChanged;
        StopProcessMonitoring();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        _gameStateService.GameStateChanged -= OnGameStateChanged;
        await StopProcessMonitoringAsync();
        _disposed = true;
    }
}

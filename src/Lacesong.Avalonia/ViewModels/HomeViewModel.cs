using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Avalonia.Services;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly IGameStateService _gameStateService;
    private readonly IGameDetector _gameDetector;
    private readonly IModManager _modManager;
    private readonly IDialogService _dialogService;
    private readonly IGameLauncher _gameLauncher;


    [ObservableProperty]
    private string _gameStatusText;

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
        IGameLauncher gameLauncher) : base(logger)
    {
        _navigationService = navigationService;
        _gameStateService = gameStateService;
        _gameDetector = gameDetector;
        _modManager = modManager;
        _dialogService = dialogService;
        _gameLauncher = gameLauncher;

        _gameStateService.GameStateChanged += OnGameStateChanged;
        
        _ = InitialGameDetectionAsync();
    }

    private async Task InitialGameDetectionAsync()
    {
        GameStatusText = "Attempting to automatically detect game...";
        if (!IsGameDetected)
        {
            await DetectGameCommand.ExecuteAsync(null);
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
            GameStatusText = "Game path not detected. Please select manually.";
        }
    }

    [RelayCommand]
    private void GoToBrowseMods() => _navigationService.NavigateTo<BrowseModsViewModel>();

    private bool CanExecuteGameCommands() => IsGameDetected;

    [RelayCommand(CanExecute = nameof(CanExecuteGameCommands))]
    private async Task LaunchModded()
    {
        await ExecuteAsync(async () =>
        {
            if (!IsGameDetected)
            {
                SetStatus("Game not detected.");
                return;
            }
            var result = await _gameLauncher.LaunchModded(CurrentGame);
            SetStatus(result.Success ? "Launched modded game." : $"Failed to launch: {result.Message}");
        }, "Launching modded game...");
    }

    [RelayCommand(CanExecute = nameof(CanExecuteGameCommands))]
    private async Task LaunchVanilla()
    {
        await ExecuteAsync(async () =>
        {
            if (!IsGameDetected)
            {
                SetStatus("Game not detected.");
                return;
            }
            var result = await _gameLauncher.LaunchVanilla(CurrentGame);
            SetStatus(result.Success ? "Launched vanilla game." : $"Failed to launch: {result.Message}");
        }, "Launching vanilla game...");
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

    [RelayCommand(CanExecute = nameof(CanExecuteGameCommands))]
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

    [RelayCommand(CanExecute = nameof(CanExecuteGameCommands))]
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
}

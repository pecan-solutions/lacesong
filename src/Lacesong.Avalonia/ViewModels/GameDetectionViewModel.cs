using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Avalonia.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.ViewModels;

public partial class GameDetectionViewModel : BaseViewModel
{
    private readonly IGameDetector _gameDetector;
    private readonly IGameStateService _gameStateService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private List<GameInstallation> _detectedGames = new();

    [ObservableProperty]
    private GameInstallation? _selectedGame;

    [ObservableProperty]
    private string _detectionStatus = "Ready to scan for games";

    // explicitly define commands to ensure they're always available
    public IAsyncRelayCommand DetectGamesAsyncCommand { get; }
    public IAsyncRelayCommand BrowseForGameFileAsyncCommand { get; }
    public IRelayCommand SetSelectedGameCommand { get; }

    public GameDetectionViewModel(
        ILogger<GameDetectionViewModel> logger,
        IGameDetector gameDetector,
        IGameStateService gameStateService,
        IDialogService dialogService) : base(logger)
    {
        _gameDetector = gameDetector;
        _gameStateService = gameStateService;
        _dialogService = dialogService;

        // initialize commands
        DetectGamesAsyncCommand = new AsyncRelayCommand(DetectGamesAsync);
        BrowseForGameFileAsyncCommand = new AsyncRelayCommand(BrowseForGameFileAsync);
        SetSelectedGameCommand = new RelayCommand(SetSelectedGame);
        
        // load existing game from state if present
        InitializeFromGameState();
    }
    
    private void InitializeFromGameState()
    {
        if (_gameStateService.IsGameDetected && _gameStateService.CurrentGame != null)
        {
            var currentGame = _gameStateService.CurrentGame;
            DetectedGames = new List<GameInstallation> { currentGame };
            SelectedGame = currentGame;
            DetectionStatus = $"{currentGame.Name} is currently set";
        }
    }

    private async Task DetectGamesAsync()
    {
        await ExecuteAsync(async () =>
        {
            DetectionStatus = "Scanning for game installations...";
            
            var games = await _gameDetector.GetSupportedGames();
            var newlyDetectedGames = new List<GameInstallation>();

            foreach (var game in games)
            {
                var detectedGame = await _gameDetector.DetectGameInstall();
                if (detectedGame != null && detectedGame.Name == game.Name)
                {
                    // only add if not already in the list
                    if (!DetectedGames.Any(g => g.InstallPath == detectedGame.InstallPath))
                    {
                        newlyDetectedGames.Add(detectedGame);
                    }
                }
            }
            
            // merge newly detected games with existing ones
            if (newlyDetectedGames.Any())
            {
                var mergedGames = new List<GameInstallation>(DetectedGames);
                mergedGames.AddRange(newlyDetectedGames);
                DetectedGames = mergedGames;
                
                DetectionStatus = $"Found {newlyDetectedGames.Count} new game installation(s). Total: {DetectedGames.Count}";
                if (SelectedGame == null)
                {
                    SelectedGame = DetectedGames.First();
                }
            }
            else if (DetectedGames.Any())
            {
                DetectionStatus = $"No new games found. {DetectedGames.Count} game(s) already detected";
            }
            else
            {
                DetectionStatus = "No game installations found";
            }
        }, "Detecting games...");
    }

    private async Task BrowseForGameFileAsync()
    {
        // get platform-specific file filter
        var filter = GetPlatformFileFilter();
        
        var selectedFile = await _dialogService.ShowOpenFileDialogAsync("Select Game Executable", filter);
        if (!string.IsNullOrEmpty(selectedFile))
        {
            await ExecuteAsync(async () =>
            {
                DetectionStatus = "Validating selected executable...";
                
                // get the directory containing the executable
                var directory = Path.GetDirectoryName(selectedFile);
                if (string.IsNullOrEmpty(directory))
                {
                    DetectionStatus = "Invalid executable path";
                    return;
                }
                
                var game = await _gameDetector.DetectGameInstall(directory);
                if (game != null)
                {
                    if (!DetectedGames.Any(g => g.InstallPath == game.InstallPath))
                    {
                        DetectedGames.Add(game);
                        DetectedGames = new List<GameInstallation>(DetectedGames); // Refresh list
                    }
                    
                    SelectedGame = game;
                    DetectionStatus = $"Validated {game.Name} installation";
                }
                else
                {
                    DetectionStatus = "Invalid game executable selected";
                }
            }, "Validating game executable...");
        }
    }
    
    private string GetPlatformFileFilter()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Executable Files|*.exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "Application Bundles|*.app";
        }
        else // Linux and others
        {
            return "All Files|*";
        }
    }

    private void SetSelectedGame()
    {
        if (SelectedGame != null)
        {
            _gameStateService.SetCurrentGame(SelectedGame);
            DetectionStatus = $"{SelectedGame.Name} has been set as the active game.";
        }
    }
}

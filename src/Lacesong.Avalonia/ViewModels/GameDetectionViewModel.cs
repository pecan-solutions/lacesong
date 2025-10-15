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

    // called when SelectedGame property changes (by CommunityToolkit.Mvvm)
    partial void OnSelectedGameChanged(GameInstallation? value)
    {
        // when the user selects a game from the list, update the game state
        if (value != null)
        {
            _gameStateService.SetCurrentGame(value);
            DetectionStatus = $"{value.Name} is now active";
        }
    }

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
            
            // use the new method to detect all game installations at once
            var allDetectedGames = await _gameDetector.DetectAllGameInstalls();
            
            // merge with existing games (avoid duplicates)
            var existingPaths = DetectedGames.Select(g => g.InstallPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newlyDetectedGames = allDetectedGames.Where(g => !existingPaths.Contains(g.InstallPath)).ToList();
            
            if (newlyDetectedGames.Any())
            {
                var mergedGames = new List<GameInstallation>(DetectedGames);
                mergedGames.AddRange(newlyDetectedGames);
                DetectedGames = mergedGames;
                
                DetectionStatus = $"Found {newlyDetectedGames.Count} new game installation(s). Total: {DetectedGames.Count}";
                
                // auto-select the first game if none selected, and set it in game state
                if (SelectedGame == null && DetectedGames.Any())
                {
                    SelectedGame = DetectedGames.First();
                    _gameStateService.SetCurrentGame(SelectedGame);
                    DetectionStatus = $"{SelectedGame.Name} selected and ready to use";
                }
            }
            else if (DetectedGames.Any())
            {
                DetectionStatus = $"No new games found. {DetectedGames.Count} game(s) already detected";
            }
            else
            {
                DetectionStatus = "No game installations found. Try manual selection.";
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
                    _gameStateService.SetCurrentGame(game);
                    DetectionStatus = $"{game.Name} selected and ready to use";
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
        // this method is kept for explicit button clicks, but selection now also happens automatically
        // via OnSelectedGameChanged when the user clicks on a list item
        if (SelectedGame != null)
        {
            _gameStateService.SetCurrentGame(SelectedGame);
            DetectionStatus = $"{SelectedGame.Name} is now active and ready to use";
        }
    }
}

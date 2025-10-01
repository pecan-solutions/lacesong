using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Avalonia.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public IAsyncRelayCommand BrowseForGameAsyncCommand { get; }
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
        BrowseForGameAsyncCommand = new AsyncRelayCommand(BrowseForGameAsync);
        SetSelectedGameCommand = new RelayCommand(SetSelectedGame);
    }

    private async Task DetectGamesAsync()
    {
        await ExecuteAsync(async () =>
        {
            DetectionStatus = "Scanning for game installations...";
            
            var games = await _gameDetector.GetSupportedGames();
            var currentDetectedGames = new List<GameInstallation>();

            foreach (var game in games)
            {
                var detectedGame = await _gameDetector.DetectGameInstall();
                if (detectedGame != null && detectedGame.Name == game.Name)
                {
                    currentDetectedGames.Add(detectedGame);
                }
            }
            
            DetectedGames = currentDetectedGames;

            if (DetectedGames.Any())
            {
                DetectionStatus = $"Found {DetectedGames.Count} game installation(s)";
                SelectedGame = DetectedGames.First();
            }
            else
            {
                DetectionStatus = "No game installations found";
            }
        }, "Detecting games...");
    }

    private async Task BrowseForGameAsync()
    {
        var selectedPath = await _dialogService.ShowFolderDialogAsync("Select Game Directory");
        if (!string.IsNullOrEmpty(selectedPath))
        {
            await ExecuteAsync(async () =>
            {
                DetectionStatus = "Validating selected directory...";
                
                var game = await _gameDetector.DetectGameInstall(selectedPath);
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
                    DetectionStatus = "Invalid game directory selected";
                }
            }, "Validating game directory...");
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

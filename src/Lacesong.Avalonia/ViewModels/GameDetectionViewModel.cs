using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.ViewModels;

public partial class GameDetectionViewModel : BaseViewModel
{
    private readonly IGameDetector _gameDetector;
    // private readonly IDialogService _dialogService;
    // private readonly ISnackbarService _snackbarService;

    [ObservableProperty]
    private List<GameInstallation> _detectedGames = new();

    [ObservableProperty]
    private GameInstallation? _selectedGame;

    [ObservableProperty]
    private bool _isDetecting;

    [ObservableProperty]
    private string _detectionStatus = "Click 'Detect Games' to scan for installations";

    public GameDetectionViewModel(
        ILogger<GameDetectionViewModel> logger,
        IGameDetector gameDetector
        /*IDialogService dialogService,
        ISnackbarService snackbarService*/) : base(logger)
    {
        _gameDetector = gameDetector;
        // _dialogService = dialogService;
        // _snackbarService = snackbarService;
    }

    [RelayCommand]
    private async Task DetectGamesAsync()
    {
        await ExecuteAsync(async () =>
        {
            IsDetecting = true;
            DetectionStatus = "Scanning for game installations...";
            
            var games = await _gameDetector.GetSupportedGames();
            var currentDetectedGames = new List<GameInstallation>();

            foreach (var game in games)
            {
                DetectionStatus = $"Checking for {game.Name}...";
                var detectedGame = await _gameDetector.DetectGameInstall();
                if (detectedGame != null && detectedGame.Name == game.Name)
                {
                    currentDetectedGames.Add(detectedGame);
                }
            }
            
            DetectedGames = currentDetectedGames;

            if (DetectedGames.Count > 0)
            {
                DetectionStatus = $"Found {DetectedGames.Count} game installation(s)";
                SelectedGame = DetectedGames.First();
                /*_snackbarService.Show(
                    "Success", 
                    DetectionStatus, 
                    "Success", 
                    "✅", 
                    TimeSpan.FromSeconds(3));*/
            }
            else
            {
                DetectionStatus = "No game installations found";
                /*_snackbarService.Show(
                    "Not Found", 
                    DetectionStatus, 
                    "Warning", 
                    "❓", 
                    TimeSpan.FromSeconds(3));*/
            }
        }, "Detecting games...");
        
        IsDetecting = false;
    }

    [RelayCommand]
    private async Task BrowseForGameAsync()
    {
        // var selectedPath = await _dialogService.ShowFolderDialogAsync("Select Game Directory");
        var selectedPath = "/mock/path"; // Placeholder
        if (!string.IsNullOrEmpty(selectedPath))
        {
            await ExecuteAsync(async () =>
            {
                DetectionStatus = "Validating selected directory...";
                
                var game = await _gameDetector.DetectGameInstall(selectedPath);
                if (game != null)
                {
                    var currentDetectedGames = new List<GameInstallation>(DetectedGames);
                    if (!currentDetectedGames.Any(g => g.InstallPath == game.InstallPath))
                    {
                        currentDetectedGames.Add(game);
                    }
                    
                    DetectedGames = currentDetectedGames;
                    SelectedGame = game;
                    DetectionStatus = $"Validated {game.Name} installation";
                }
                else
                {
                    DetectionStatus = "Invalid game directory selected";
                    SetStatus("Invalid game directory selected", true);
                }
            });
        }
    }

    [RelayCommand]
    private void SelectGame(GameInstallation game)
    {
        SelectedGame = game;
        DetectionStatus = $"Selected {game.Name}";
    }

    private bool CanProceedToNext()
    {
        return SelectedGame != null && SelectedGame.IsValid;
    }

    [RelayCommand(CanExecute = nameof(CanProceedToNext))]
    private void ProceedToNext()
    {
        if (CanProceedToNext())
        {
            Logger.LogInformation($"Proceeding with game: {SelectedGame!.Name}");
        }
    }
}

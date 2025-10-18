using CommunityToolkit.Mvvm.ComponentModel;
using Lacesong.Core.Models;
using Lacesong.Core.Services;
using System;
using Lacesong.Core.Interfaces;

namespace Lacesong.Avalonia.Services;

public partial class GameStateService : ObservableObject, IGameStateService
{
    private readonly IModUpdateService _updateService;

    public GameStateService(IModUpdateService updateService)
    {
        _updateService = updateService;
    }

    [ObservableProperty]
    private GameInstallation _currentGame = new();

    public bool IsGameDetected => !string.IsNullOrEmpty(CurrentGame.InstallPath);

    public event Action? GameStateChanged;

    public async void SetCurrentGame(GameInstallation game)
    {
        CurrentGame = game;
        
        // ensure mods directory exists for this installation
        // this is critical for all downstream operations (mod installation, BepInEx, etc.)
        if (!string.IsNullOrEmpty(game.InstallPath))
        {
            ModManager.EnsureModsDirectory(game);
            // run startup update check (non-forced, uses cache)
            _ = _updateService.CheckForUpdates(game);
            await _updateService.ScheduleUpdateChecks(game);
        }
        
        GameStateChanged?.Invoke();
        OnPropertyChanged(nameof(IsGameDetected));
    }
}

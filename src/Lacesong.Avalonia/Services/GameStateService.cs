using CommunityToolkit.Mvvm.ComponentModel;
using Lacesong.Core.Models;
using Lacesong.Core.Services;
using System;

namespace Lacesong.Avalonia.Services;

public partial class GameStateService : ObservableObject, IGameStateService
{
    [ObservableProperty]
    private GameInstallation _currentGame = new();

    public bool IsGameDetected => !string.IsNullOrEmpty(CurrentGame.InstallPath);

    public event Action? GameStateChanged;

    public void SetCurrentGame(GameInstallation game)
    {
        CurrentGame = game;
        
        // ensure mods directory exists for this installation
        // this is critical for all downstream operations (mod installation, BepInEx, etc.)
        if (!string.IsNullOrEmpty(game.InstallPath))
        {
            ModManager.EnsureModsDirectory(game);
        }
        
        GameStateChanged?.Invoke();
        OnPropertyChanged(nameof(IsGameDetected));
    }
}

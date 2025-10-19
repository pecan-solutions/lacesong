using CommunityToolkit.Mvvm.ComponentModel;
using Lacesong.Core.Models;
using Lacesong.Core.Services;
using System;
using System.Threading.Tasks;
using Lacesong.Core.Interfaces;

namespace Lacesong.Avalonia.Services;

public partial class GameStateService : ObservableObject, IGameStateService
{
    private readonly IModUpdateService _updateService;
    private readonly IBepInExManager _bepInExManager;

    public GameStateService(IModUpdateService updateService, IBepInExManager bepInExManager)
    {
        _updateService = updateService;
        _bepInExManager = bepInExManager;
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
            
            // run startup update checks (non-forced, uses cache)
            _ = _updateService.CheckForUpdates(game);
            await _updateService.ScheduleUpdateChecks(game);
            
            // check for BepInEx updates in the background
            _ = CheckForBepInExUpdatesAsync(game);
            
            // cleanup old backups in the background
            _ = CleanupOldBackupsAsync(game);
        }
        
        GameStateChanged?.Invoke();
        OnPropertyChanged(nameof(IsGameDetected));
    }

    private async Task CheckForBepInExUpdatesAsync(GameInstallation game)
    {
        try
        {
            var update = await _bepInExManager.CheckForBepInExUpdates(game);
            if (update != null)
            {
                // notify user about available BepInEx update
                OnBepInExUpdateAvailable?.Invoke(update);
            }
        }
        catch (Exception ex)
        {
            // log error but don't throw - this is a background check
            Console.WriteLine($"Error checking for BepInEx updates: {ex.Message}");
        }
    }

    private async Task CleanupOldBackupsAsync(GameInstallation game)
    {
        try
        {
            var result = await _bepInExManager.CleanupBepInExBackups(game);
            if (result.Success && result.Data is BackupCleanupResult data)
            {
                // only log if we actually cleaned up something
                if (data.DeletedCount > 0)
                {
                    Console.WriteLine($"Startup backup cleanup: {result.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during startup backup cleanup: {ex.Message}");
        }
    }

    public event Action<BepInExUpdate>? OnBepInExUpdateAvailable;
}

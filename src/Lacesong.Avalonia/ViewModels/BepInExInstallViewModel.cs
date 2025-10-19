using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Avalonia.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.ViewModels;

public partial class BepInExInstallViewModel : BaseViewModel, IDisposable
{
    private readonly IBepInExManager _bepinexManager;
    private readonly IBepInExVersionCacheService _versionCacheService;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IGameStateService _gameStateService;
    private bool _disposed;

    [ObservableProperty]
    private GameInstallation? _gameInstallation;

    [ObservableProperty]
    private bool _isBepInExInstalled;

    [ObservableProperty]
    private string _installedVersion = string.Empty;

    [ObservableProperty]
    private string _latestVersion = string.Empty;

    [ObservableProperty]
    private bool _forceReinstall;

    partial void OnForceReinstallChanged(bool value)
    {
        InstallLatestCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private bool _createBackup = true;

    [ObservableProperty]
    private string _installationStatus = "Ready";

    [ObservableProperty]
    private bool _isRefreshingVersion;

    public BepInExInstallViewModel(
        ILogger<BepInExInstallViewModel> logger,
        IBepInExManager bepinexManager,
        IBepInExVersionCacheService versionCacheService,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        IGameStateService gameStateService) : base(logger)
    {
        _bepinexManager = bepinexManager;
        _versionCacheService = versionCacheService;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _gameStateService = gameStateService;
        
        _gameInstallation = _gameStateService.CurrentGame;
        _gameStateService.GameStateChanged += OnGameStateChanged;

        CheckBepInExStatus();
        _ = FetchLatestBepInExVersion();
    }

    private void OnGameStateChanged()
    {
        GameInstallation = _gameStateService.CurrentGame;
        CheckBepInExStatus();
        InstallLatestCommand.NotifyCanExecuteChanged();
        UninstallBepInExCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void CheckBepInExStatus()
    {
        if (GameInstallation == null || string.IsNullOrEmpty(GameInstallation.InstallPath))
        {
            IsBepInExInstalled = false;
            InstalledVersion = string.Empty;
            InstallationStatus = "No game selected";
            InstallLatestCommand.NotifyCanExecuteChanged();
            UninstallBepInExCommand.NotifyCanExecuteChanged();
            return;
        }

        IsBepInExInstalled = _bepinexManager.IsBepInExInstalled(GameInstallation);
        
        if (IsBepInExInstalled)
        {
            InstalledVersion = _bepinexManager.GetInstalledBepInExVersion(GameInstallation) ?? "Unknown";
            InstallationStatus = $"BepInEx {InstalledVersion} is installed";
        }
        else
        {
            InstalledVersion = string.Empty;
            InstallationStatus = "BepInEx is not installed";
        }
        
        InstallLatestCommand.NotifyCanExecuteChanged();
        UninstallBepInExCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanInstallBepInEx))]
    private async Task InstallLatest()
    {
        if (GameInstallation == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Install BepInEx",
            $"Are you sure you want to install BepInEx {LatestVersion} to {GameInstallation.Name}?");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            var options = new BepInExInstallOptions
            {
                Version = LatestVersion,
                ForceReinstall = ForceReinstall,
                BackupExisting = CreateBackup
            };

            var installResult = await _bepinexManager.InstallBepInEx(GameInstallation, options);
            
            if (installResult.Success)
            {
                _snackbarService.Show(
                    "Success", 
                    "BepInEx was installed successfully.", 
                    "Success");
                CheckBepInExStatus();
            }
            else
            {
                _snackbarService.Show(
                    "Installation Failed", 
                    installResult.Error ?? "Unknown error occurred", 
                    "Error");
            }
        }, "Installing BepInEx...");
    }

    [RelayCommand]
    private async Task UninstallBepInEx()
    {
        if (GameInstallation == null || !IsBepInExInstalled) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Uninstall BepInEx",
            $"Are you sure you want to uninstall BepInEx from {GameInstallation.Name}? This will remove all mods and configurations.");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            var uninstallResult = await _bepinexManager.UninstallBepInEx(GameInstallation);
            
            if (uninstallResult.Success)
            {
                _snackbarService.Show(
                    "Success", 
                    "BepInEx was uninstalled successfully.", 
                    "Success");
                CheckBepInExStatus();
            }
            else
            {
                _snackbarService.Show(
                    "Uninstallation Failed", 
                    uninstallResult.Error ?? "Unknown error occurred", 
                    "Error");
            }
        }, "Uninstalling BepInEx...");
    }

    private bool CanInstallBepInEx()
    {
        return GameInstallation != null && (!IsBepInExInstalled || ForceReinstall);
    }

    private bool CanUninstallBepInEx()
    {
        return GameInstallation != null && IsBepInExInstalled;
    }

    [RelayCommand]
    private async Task RefreshVersion()
    {
        if (IsRefreshingVersion) return;

        IsRefreshingVersion = true;
        try
        {
            // invalidate cache to force fresh fetch
            _versionCacheService.InvalidateCache();
            
            // fetch latest version
            await FetchLatestBepInExVersion();
            
            _snackbarService.Show(
                "Version Refreshed", 
                "BepInEx version information has been refreshed.", 
                "Success");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error refreshing BepInEx version");
            _snackbarService.Show(
                "Refresh Failed", 
                "Failed to refresh BepInEx version information.", 
                "Error");
        }
        finally
        {
            IsRefreshingVersion = false;
        }
    }

    private async Task FetchLatestBepInExVersion()
    {
        try
        {
            var latestVersion = await _versionCacheService.GetLatestVersionAsync();
            
            if (!string.IsNullOrEmpty(latestVersion))
            {
                LatestVersion = latestVersion;
                Logger.LogInformation($"Latest BepInEx version: {LatestVersion}");
            }
            else
            {
                Logger.LogWarning("Failed to fetch latest BepInEx version from cache service");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching latest BepInEx version");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _gameStateService.GameStateChanged -= OnGameStateChanged;
        _disposed = true;
    }
}

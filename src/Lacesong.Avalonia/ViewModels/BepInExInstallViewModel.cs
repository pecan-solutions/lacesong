using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Avalonia.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.ViewModels;

public partial class BepInExInstallViewModel : BaseViewModel, IDisposable
{
    private readonly IBepInExManager _bepinexManager;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IGameStateService _gameStateService;
    private readonly HttpClient _httpClient;
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

    public BepInExInstallViewModel(
        ILogger<BepInExInstallViewModel> logger,
        IBepInExManager bepinexManager,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        IGameStateService gameStateService) : base(logger)
    {
        _bepinexManager = bepinexManager;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _gameStateService = gameStateService;
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Lacesong-ModManager/1.0.0");
        
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
                    installResult.Error, 
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
                    uninstallResult.Error, 
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

    private async Task FetchLatestBepInExVersion()
    {
        try
        {
            using var response = await _httpClient.GetAsync("https://api.github.com/repos/BepInEx/BepInEx/releases/latest");
            
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Failed to fetch latest BepInEx version from GitHub API");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            
            if (document.RootElement.TryGetProperty("tag_name", out var tagNameElement))
            {
                var tagName = tagNameElement.GetString();
                if (!string.IsNullOrEmpty(tagName))
                {
                    // remove 'v' prefix if present (e.g., "v5.4.23.4" -> "5.4.23.4")
                    LatestVersion = tagName.TrimStart('v');
                    
                    Logger.LogInformation($"Latest BepInEx version: {LatestVersion}");
                }
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
        _httpClient?.Dispose();
        _disposed = true;
    }
}

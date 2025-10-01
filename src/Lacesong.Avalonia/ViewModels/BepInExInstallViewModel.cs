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

public partial class BepInExInstallViewModel : BaseViewModel
{
    private readonly IBepInExManager _bepinexManager;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IGameStateService _gameStateService;

    [ObservableProperty]
    private GameInstallation? _gameInstallation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallBepInExCommand))]
    [NotifyCanExecuteChangedFor(nameof(UninstallBepInExCommand))]
    private bool _isBepInExInstalled;

    [ObservableProperty]
    private string _installedVersion = string.Empty;

    [ObservableProperty]
    private string _selectedVersion = "5.4.22";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallBepInExCommand))]
    private bool _forceReinstall;

    [ObservableProperty]
    private bool _createBackup = true;

    [ObservableProperty]
    private List<string> _availableVersions = new()
    {
        "5.4.22",
        "5.4.21",
        "5.4.20",
        "5.4.19"
    };

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
        
        _gameInstallation = _gameStateService.CurrentGame;
        _gameStateService.GameStateChanged += OnGameStateChanged;

        CheckBepInExStatus();
    }

    private void OnGameStateChanged()
    {
        GameInstallation = _gameStateService.CurrentGame;
        CheckBepInExStatus();
    }

    [RelayCommand]
    private void CheckBepInExStatus()
    {
        if (GameInstallation == null || string.IsNullOrEmpty(GameInstallation.InstallPath))
        {
            IsBepInExInstalled = false;
            InstalledVersion = string.Empty;
            InstallationStatus = "No game selected";
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
    }

    [RelayCommand(CanExecute = nameof(CanInstallBepInEx))]
    private async Task InstallBepInEx()
    {
        if (GameInstallation == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Install BepInEx",
            $"Are you sure you want to install BepInEx {SelectedVersion} to {GameInstallation.Name}?");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            var options = new BepInExInstallOptions
            {
                Version = SelectedVersion,
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

    [RelayCommand(CanExecute = nameof(CanUninstallBepInEx))]
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
}

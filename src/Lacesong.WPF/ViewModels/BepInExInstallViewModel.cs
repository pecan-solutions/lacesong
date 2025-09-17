using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.WPF.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Lacesong.WPF.ViewModels;

/// <summary>
/// view model for bepinex installation flow
/// </summary>
public partial class BepInExInstallViewModel : BaseViewModel
{
    private readonly IBepInExManager _bepinexManager;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;

    [ObservableProperty]
    private GameInstallation? _gameInstallation;

    [ObservableProperty]
    private bool _isBepInExInstalled;

    [ObservableProperty]
    private string _installedVersion = string.Empty;

    [ObservableProperty]
    private string _selectedVersion = "5.4.22";

    [ObservableProperty]
    private bool _forceReinstall;

    [ObservableProperty]
    private bool _createBackup = true;

    [ObservableProperty]
    private bool _createDesktopShortcut;

    [ObservableProperty]
    private List<string> _availableVersions = new()
    {
        "5.4.22",
        "5.4.21",
        "5.4.20",
        "5.4.19"
    };

    [ObservableProperty]
    private string _installationStatus = "Ready to install BepInEx";

    public BepInExInstallViewModel(
        ILogger<BepInExInstallViewModel> logger,
        IBepInExManager bepinexManager,
        IDialogService dialogService,
        ISnackbarService snackbarService) : base(logger)
    {
        _bepinexManager = bepinexManager;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
    }

    public void SetGameInstallation(GameInstallation gameInstallation)
    {
        GameInstallation = gameInstallation;
        CheckBepInExStatus();
    }

    [RelayCommand]
    private void CheckBepInExStatus()
    {
        if (GameInstallation == null) return;

        IsBepInExInstalled = _bepinexManager.IsBepInExInstalled(GameInstallation);
        
        if (IsBepInExInstalled)
        {
            InstalledVersion = _bepinexManager.GetInstalledBepInExVersion(GameInstallation) ?? "Unknown";
            InstallationStatus = $"BepInEx {InstalledVersion} is already installed";
        }
        else
        {
            InstalledVersion = string.Empty;
            InstallationStatus = "BepInEx is not installed";
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallBepInEx))]
    private async Task InstallBepInExAsync()
    {
        if (GameInstallation == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Install BepInEx",
            $"Are you sure you want to install BepInEx {SelectedVersion} to {GameInstallation.Name}?");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            InstallationStatus = "Installing BepInEx...";
            
            var options = new BepInExInstallOptions
            {
                Version = SelectedVersion,
                ForceReinstall = ForceReinstall,
                BackupExisting = CreateBackup,
                CreateDesktopShortcut = CreateDesktopShortcut
            };

            var installResult = await _bepinexManager.InstallBepInEx(GameInstallation, options);
            
            if (installResult.Success)
            {
                InstallationStatus = "BepInEx installed successfully";
                _snackbarService.Show(
                    "Success", 
                    "BepInEx was installed successfully.", 
                    ControlAppearance.Success, 
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24), 
                    TimeSpan.FromSeconds(3));
                CheckBepInExStatus();
            }
            else
            {
                InstallationStatus = $"Installation failed: {installResult.Error}";
                _snackbarService.Show(
                    "Installation Failed", 
                    installResult.Error, 
                    ControlAppearance.Danger, 
                    new SymbolIcon(SymbolRegular.ErrorCircle24), 
                    TimeSpan.FromSeconds(5));
            }
        }, "Installing BepInEx...");
    }

    [RelayCommand(CanExecute = nameof(CanUninstallBepInEx))]
    private async Task UninstallBepInExAsync()
    {
        if (GameInstallation == null || !IsBepInExInstalled) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Uninstall BepInEx",
            $"Are you sure you want to uninstall BepInEx from {GameInstallation.Name}? This will remove all mods and configurations.");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            InstallationStatus = "Uninstalling BepInEx...";
            
            var uninstallResult = await _bepinexManager.UninstallBepInEx(GameInstallation);
            
            if (uninstallResult.Success)
            {
                InstallationStatus = "BepInEx uninstalled successfully";
                _snackbarService.Show(
                    "Success", 
                    "BepInEx was uninstalled successfully.", 
                    ControlAppearance.Success, 
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24), 
                    TimeSpan.FromSeconds(3));
                CheckBepInExStatus();
            }
            else
            {
                InstallationStatus = $"Uninstallation failed: {uninstallResult.Error}";
                _snackbarService.Show(
                    "Uninstallation Failed", 
                    uninstallResult.Error, 
                    ControlAppearance.Danger, 
                    new SymbolIcon(SymbolRegular.ErrorCircle24), 
                    TimeSpan.FromSeconds(5));
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
    private void RefreshStatus()
    {
        CheckBepInExStatus();
    }
}

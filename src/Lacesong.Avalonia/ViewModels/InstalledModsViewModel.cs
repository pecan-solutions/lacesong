using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Core.Services;
using Lacesong.Avalonia.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.IO;

namespace Lacesong.Avalonia.ViewModels;

public partial class InstalledModsViewModel : BaseViewModel
{
    private readonly IModManager _modManager;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IGameStateService _gameStateService;

    [ObservableProperty]
    private GameInstallation? _gameInstallation;

    [ObservableProperty]
    private ObservableCollection<ModInfo> _installedMods = new();

    [ObservableProperty]
    private ModInfo? _selectedInstalledMod;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showEnabledOnly = false;

    public InstalledModsViewModel(
        ILogger<InstalledModsViewModel> logger,
        IModManager modManager,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        IGameStateService gameStateService) : base(logger)
    {
        _modManager = modManager;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _gameStateService = gameStateService;

        GameInstallation = _gameStateService.CurrentGame;
        _gameStateService.GameStateChanged += OnGameStateChanged;

        _ = RefreshModsAsync();
    }

    private void OnGameStateChanged()
    {
        GameInstallation = _gameStateService.CurrentGame;
        _ = RefreshModsAsync();
    }

    [RelayCommand]
    private async Task RefreshModsAsync()
    {
        if (GameInstallation == null || string.IsNullOrEmpty(GameInstallation.InstallPath))
        {
            InstalledMods.Clear();
            return;
        }

        await ExecuteAsync(async () =>
        {
            var mods = await _modManager.GetInstalledMods(GameInstallation);
            
            InstalledMods.Clear();
            foreach (var mod in mods)
            {
                InstalledMods.Add(mod);
            }
            
            SetStatus($"Loaded {mods.Count} installed mods");
        }, "Refreshing mods...");
    }

    [RelayCommand]
    private async Task InstallModFromFileCommand()
    {
        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            "Select Mod File",
            "Zip Files (*.zip)|*.zip|All Files (*.*)|*.*");
            
        if (string.IsNullOrEmpty(filePath) || GameInstallation == null) return;

        await ExecuteAsync(async () =>
        {
            var result = await _modManager.InstallModFromZip(filePath, GameInstallation);
            
            if (result.Success)
            {
                SetStatus("Mod installed successfully");
                _snackbarService.Show(
                    "Success", 
                    $"Successfully installed {Path.GetFileName(filePath)}.", 
                    "Success");
                await RefreshModsAsync();
            }
            else
            {
                SetStatus($"Installation failed: {result.Error}", true);
                _snackbarService.Show(
                    "Installation Failed", 
                    result.Error ?? "Unknown error occurred", 
                    "Error");
            }
        }, "Installing mod...");
    }

    [RelayCommand]
    private async Task InstallModFromUrlCommand()
    {
        var url = await _dialogService.ShowInputDialogAsync(
            "Install Mod from URL",
            "Enter the download URL for the mod:",
            "https://");
            
        if (string.IsNullOrEmpty(url) || GameInstallation == null) return;

        await ExecuteAsync(async () =>
        {
            var result = await _modManager.InstallModFromZip(url, GameInstallation);
            
            if (result.Success)
            {
                SetStatus("Mod installed successfully");
                await RefreshModsAsync();
            }
            else
            {
                SetStatus($"Installation failed: {result.Error}", true);
            }
        }, "Installing mod from URL...");
    }

    [RelayCommand]
    private async Task UninstallModCommand(ModInfo mod)
    {
        if (mod == null || GameInstallation == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Uninstall Mod",
            $"Are you sure you want to uninstall '{mod.Name}'? This action cannot be undone.");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            var uninstallResult = await _modManager.UninstallMod(mod.Id, GameInstallation);
            
            if (uninstallResult.Success)
            {
                SetStatus("Mod uninstalled successfully");
                _snackbarService.Show(
                    "Success", 
                    $"Successfully uninstalled {mod.Name}.", 
                    "Success");
                await RefreshModsAsync();
            }
            else
            {
                SetStatus($"Uninstallation failed: {uninstallResult.Error}", true);
                _snackbarService.Show(
                    "Uninstallation Failed", 
                    uninstallResult.Error ?? "Unknown error occurred", 
                    "Error");
            }
        }, "Uninstalling mod...");
    }

    [RelayCommand]
    private async Task ToggleModEnabledCommand(ModInfo mod)
    {
        if (mod == null || GameInstallation == null) return;

        await ExecuteAsync(async () =>
        {
            var result = mod.IsEnabled 
                ? await _modManager.DisableMod(mod.Id, GameInstallation)
                : await _modManager.EnableMod(mod.Id, GameInstallation);
            
            if (result.Success)
            {
                SetStatus($"Mod {(mod.IsEnabled ? "disabled" : "enabled")} successfully");
                _snackbarService.Show(
                    "Success", 
                    $"{mod.Name} has been {(mod.IsEnabled ? "disabled" : "enabled")}.", 
                    "Success");
                await RefreshModsAsync();
            }
            else
            {
                SetStatus($"Failed to {(mod.IsEnabled ? "disable" : "enable")} mod: {result.Error}", true);
                _snackbarService.Show(
                    "Error", 
                    $"Failed to {(mod.IsEnabled ? "disable" : "enable")} mod: {result.Error}", 
                    "Error");
            }
        }, "Toggling mod...");
    }

    [RelayCommand]
    private void OpenModsFolderCommand()
    {
        if (GameInstallation == null) return;
        try
        {
            var modsPath = ModManager.GetModsDirectoryPath(GameInstallation);
            if (!Directory.Exists(modsPath))
            {
                Directory.CreateDirectory(modsPath);
            }
            
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = modsPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SetStatus($"failed to open mods folder: {ex.Message}", true);
            _snackbarService.Show(
                "Error", 
                $"Failed to open mods folder: {ex.Message}", 
                "Error");
        }
    }
}

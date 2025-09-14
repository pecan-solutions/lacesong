using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.WPF.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Lacesong.WPF.ViewModels;

/// <summary>
/// view model for mod catalog and management
/// </summary>
public partial class ModCatalogViewModel : BaseViewModel
{
    private readonly IModManager _modManager;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private GameInstallation? _gameInstallation;

    [ObservableProperty]
    private ObservableCollection<ModInfo> _installedMods = new();

    [ObservableProperty]
    private ObservableCollection<ModInfo> _availableMods = new();

    [ObservableProperty]
    private ModInfo? _selectedMod;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showInstalledOnly = false;

    [ObservableProperty]
    private bool _showEnabledOnly = false;

    [ObservableProperty]
    private string _modStatus = "Ready to manage mods";

    public ModCatalogViewModel(
        ILogger<ModCatalogViewModel> logger,
        IModManager modManager,
        IDialogService dialogService) : base(logger)
    {
        _modManager = modManager;
        _dialogService = dialogService;
    }

    public void SetGameInstallation(GameInstallation gameInstallation)
    {
        GameInstallation = gameInstallation;
        _ = RefreshModsAsync(); // fire and forget
    }

    [RelayCommand]
    private async Task RefreshModsAsync()
    {
        if (GameInstallation == null) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Loading installed mods...";
            
            var mods = await _modManager.GetInstalledMods(GameInstallation);
            
            InstalledMods.Clear();
            foreach (var mod in mods)
            {
                InstalledMods.Add(mod);
            }
            
            ModStatus = $"Found {mods.Count} installed mod(s)";
            SetStatus($"Loaded {mods.Count} installed mods");
        }, "Refreshing mods...");
    }

    [RelayCommand]
    private async Task InstallModFromFileAsync()
    {
        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            "Select Mod File",
            "Zip Files (*.zip)|*.zip|All Files (*.*)|*.*");
            
        if (string.IsNullOrEmpty(filePath) || GameInstallation == null) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Installing mod...";
            
            var result = await _modManager.InstallModFromZip(filePath, GameInstallation);
            
            if (result.Success)
            {
                ModStatus = "Mod installed successfully";
                SetStatus("Mod installed successfully");
                await RefreshModsAsync();
            }
            else
            {
                ModStatus = $"Installation failed: {result.Error}";
                SetStatus($"Installation failed: {result.Error}", true);
            }
        }, "Installing mod...");
    }

    [RelayCommand]
    private async Task InstallModFromUrlAsync()
    {
        var url = await _dialogService.ShowInputDialogAsync(
            "Install Mod from URL",
            "Enter the download URL for the mod:",
            "https://");
            
        if (string.IsNullOrEmpty(url) || GameInstallation == null) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Downloading and installing mod...";
            
            var result = await _modManager.InstallModFromZip(url, GameInstallation);
            
            if (result.Success)
            {
                ModStatus = "Mod installed successfully";
                SetStatus("Mod installed successfully");
                await RefreshModsAsync();
            }
            else
            {
                ModStatus = $"Installation failed: {result.Error}";
                SetStatus($"Installation failed: {result.Error}", true);
            }
        }, "Installing mod from URL...");
    }

    [RelayCommand(CanExecute = nameof(CanUninstallMod))]
    private async Task UninstallModAsync()
    {
        if (SelectedMod == null || GameInstallation == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Uninstall Mod",
            $"Are you sure you want to uninstall '{SelectedMod.Name}'? This action cannot be undone.");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = $"Uninstalling {SelectedMod.Name}...";
            
            var uninstallResult = await _modManager.UninstallMod(SelectedMod.Id, GameInstallation);
            
            if (uninstallResult.Success)
            {
                ModStatus = "Mod uninstalled successfully";
                SetStatus("Mod uninstalled successfully");
                await RefreshModsAsync();
            }
            else
            {
                ModStatus = $"Uninstallation failed: {uninstallResult.Error}";
                SetStatus($"Uninstallation failed: {uninstallResult.Error}", true);
            }
        }, "Uninstalling mod...");
    }

    [RelayCommand(CanExecute = nameof(CanEnableMod))]
    private async Task EnableModAsync()
    {
        if (SelectedMod == null || GameInstallation == null || SelectedMod.IsEnabled) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = $"Enabling {SelectedMod.Name}...";
            
            var result = await _modManager.EnableMod(SelectedMod.Id, GameInstallation);
            
            if (result.Success)
            {
                ModStatus = "Mod enabled successfully";
                SetStatus("Mod enabled successfully");
                await RefreshModsAsync();
            }
            else
            {
                ModStatus = $"Failed to enable mod: {result.Error}";
                SetStatus($"Failed to enable mod: {result.Error}", true);
            }
        }, "Enabling mod...");
    }

    [RelayCommand(CanExecute = nameof(CanDisableMod))]
    private async Task DisableModAsync()
    {
        if (SelectedMod == null || GameInstallation == null || !SelectedMod.IsEnabled) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = $"Disabling {SelectedMod.Name}...";
            
            var result = await _modManager.DisableMod(SelectedMod.Id, GameInstallation);
            
            if (result.Success)
            {
                ModStatus = "Mod disabled successfully";
                SetStatus("Mod disabled successfully");
                await RefreshModsAsync();
            }
            else
            {
                ModStatus = $"Failed to disable mod: {result.Error}";
                SetStatus($"Failed to disable mod: {result.Error}", true);
            }
        }, "Disabling mod...");
    }

    [RelayCommand]
    private void SelectMod(ModInfo mod)
    {
        SelectedMod = mod;
        ModStatus = $"Selected: {mod.Name}";
    }

    private bool CanUninstallMod()
    {
        return SelectedMod != null && SelectedMod.IsInstalled;
    }

    private bool CanEnableMod()
    {
        return SelectedMod != null && SelectedMod.IsInstalled && !SelectedMod.IsEnabled;
    }

    private bool CanDisableMod()
    {
        return SelectedMod != null && SelectedMod.IsInstalled && SelectedMod.IsEnabled;
    }

    [RelayCommand]
    private void FilterMods()
    {
        // filtering logic would be implemented here
        // for now, we'll just refresh the display
        _ = RefreshModsAsync(); // fire and forget
    }
}

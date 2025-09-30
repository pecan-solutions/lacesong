using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
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
    private readonly IModIndexService _modIndexService;
    private readonly IModUpdateService _modUpdateService;
    private readonly IConflictDetectionService _conflictService;
    private readonly ICompatibilityService _compatibilityService;
    private readonly IModConfigService _configService;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;

    [ObservableProperty]
    private GameInstallation? _gameInstallation;

    [ObservableProperty]
    private ObservableCollection<ModInfo> _installedMods = new();

    [ObservableProperty]
    private ObservableCollection<ModIndexEntry> _availableMods = new();

    [ObservableProperty]
    private ObservableCollection<ModIndexEntry> _searchResults = new();

    [ObservableProperty]
    private ModInfo? _selectedInstalledMod;

    [ObservableProperty]
    private ModIndexEntry? _selectedAvailableMod;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private ObservableCollection<string> _categories = new();

    [ObservableProperty]
    private bool _showInstalledOnly = false;

    [ObservableProperty]
    private bool _showEnabledOnly = false;

    [ObservableProperty]
    private bool _showOfficialOnly = false;

    [ObservableProperty]
    private bool _showVerifiedOnly = false;

    [ObservableProperty]
    private string _modStatus = "Ready to manage mods";

    [ObservableProperty]
    private bool _isSearching = false;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalMods = 0;

    [ObservableProperty]
    private ObservableCollection<ModUpdate> _availableUpdates = new();

    [ObservableProperty]
    private ObservableCollection<ModConflict> _detectedConflicts = new();

    [ObservableProperty]
    private ObservableCollection<ModCompatibility> _compatibilityStatuses = new();

    [ObservableProperty]
    private bool _autoUpdateEnabled = false;

    [ObservableProperty]
    private bool _conflictDetectionEnabled = true;

    [ObservableProperty]
    private string _updateStatus = "No updates available";

    [ObservableProperty]
    private string _conflictStatus = "No conflicts detected";

    public InstalledModsViewModel(
        ILogger<InstalledModsViewModel> logger,
        IModManager modManager,
        IModIndexService modIndexService,
        IModUpdateService modUpdateService,
        IConflictDetectionService conflictService,
        ICompatibilityService compatibilityService,
        IModConfigService configService,
        IDialogService dialogService,
        ISnackbarService snackbarService) : base(logger)
    {
        _modManager = modManager;
        _modIndexService = modIndexService;
        _modUpdateService = modUpdateService;
        _conflictService = conflictService;
        _compatibilityService = compatibilityService;
        _configService = configService;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        
        // initialize categories
        Categories.Add("All");
        Categories.Add("General");
        Categories.Add("UI");
        Categories.Add("Gameplay");
        Categories.Add("Graphics");
        Categories.Add("Audio");
        Categories.Add("Utility");
        Categories.Add("Developer");
    }

    public void SetGameInstallation(GameInstallation gameInstallation)
    {
        GameInstallation = gameInstallation;
        _ = RefreshModsAsync(); // fire and forget
        _ = RefreshAvailableModsAsync(); // fire and forget
        _ = CheckForUpdatesAsync(); // fire and forget
        _ = DetectConflictsAsync(); // fire and forget
        _ = CheckCompatibilityAsync(); // fire and forget
    }

    [RelayCommand]
    private async Task RefreshAvailableModsAsync()
    {
        await ExecuteAsync(async () =>
        {
            ModStatus = "Loading available mods...";
            IsSearching = true;
            
            var criteria = new ModSearchCriteria
            {
                Page = CurrentPage,
                PageSize = 20
            };
            
            var results = await _modIndexService.SearchMods(criteria);
            
            AvailableMods.Clear();
            foreach (var mod in results.Mods)
            {
                AvailableMods.Add(mod);
            }
            
            TotalMods = results.TotalCount;
            TotalPages = results.TotalPages;
            
            ModStatus = $"Found {results.TotalCount} available mod(s)";
            IsSearching = false;
        }, "Refreshing available mods...");
    }

    [RelayCommand]
    private async Task SearchModsAsync()
    {
        await ExecuteAsync(async () =>
        {
            ModStatus = "Searching mods...";
            IsSearching = true;
            
            var criteria = new ModSearchCriteria
            {
                Query = string.IsNullOrEmpty(SearchText) ? null : SearchText,
                Category = SelectedCategory == "All" ? null : SelectedCategory,
                IsOfficial = ShowOfficialOnly ? true : null,
                IsVerified = ShowVerifiedOnly ? true : null,
                Page = CurrentPage,
                PageSize = 20
            };
            
            var results = await _modIndexService.SearchMods(criteria);
            
            SearchResults.Clear();
            foreach (var mod in results.Mods)
            {
                SearchResults.Add(mod);
            }
            
            TotalMods = results.TotalCount;
            TotalPages = results.TotalPages;
            
            ModStatus = $"Found {results.TotalCount} mod(s) matching search criteria";
            IsSearching = false;
        }, "Searching mods...");
    }

    [RelayCommand]
    private async Task InstallFromIndexAsync()
    {
        if (SelectedAvailableMod == null || GameInstallation == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Install Mod",
            $"Are you sure you want to install '{SelectedAvailableMod.Name}' by {SelectedAvailableMod.Author}?");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = $"Installing {SelectedAvailableMod.Name}...";
            
            // get the latest stable version
            var latestVersion = SelectedAvailableMod.Versions
                .Where(v => !v.IsPrerelease)
                .OrderByDescending(v => v.ReleaseDate)
                .FirstOrDefault();
                
            if (latestVersion == null)
            {
                ModStatus = "No stable version available";
                SetStatus("No stable version available", true);
                return;
            }
            
            var installResult = await _modManager.InstallModFromZip(latestVersion.DownloadUrl, GameInstallation);
            
            if (installResult.Success)
            {
                ModStatus = "Mod installed successfully";
                SetStatus("Mod installed successfully");
                await RefreshModsAsync();
            }
            else
            {
                ModStatus = $"Installation failed: {installResult.Error}";
                SetStatus($"Installation failed: {installResult.Error}", true);
            }
        }, "Installing mod from index...");
    }

    [RelayCommand]
    private async Task AddCustomRepositoryAsync()
    {
        var repoUrl = await _dialogService.ShowInputDialogAsync(
            "Add Custom Repository",
            "Enter the URL of the mod repository:",
            "https://");
            
        if (string.IsNullOrEmpty(repoUrl)) return;

        var repoName = await _dialogService.ShowInputDialogAsync(
            "Repository Name",
            "Enter a name for this repository:",
            "Custom Repository");
            
        if (string.IsNullOrEmpty(repoName)) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Adding custom repository...";
            
            var repository = new ModRepository
            {
                Id = Guid.NewGuid().ToString(),
                Name = repoName,
                Url = repoUrl,
                Type = "Custom",
                IsOfficial = false,
                IsEnabled = true,
                Description = "User-added custom repository"
            };
            
            var result = await _modIndexService.AddRepository(repository);
            
            if (result.Success)
            {
                ModStatus = "Repository added successfully";
                SetStatus("Repository added successfully");
                await RefreshAvailableModsAsync();
            }
            else
            {
                ModStatus = $"Failed to add repository: {result.Error}";
                SetStatus($"Failed to add repository: {result.Error}", true);
            }
        }, "Adding custom repository...");
    }

    [RelayCommand]
    private async Task RefreshIndexAsync()
    {
        await ExecuteAsync(async () =>
        {
            ModStatus = "Refreshing mod index...";
            
            var result = await _modIndexService.RefreshIndex();
            
            if (result.Success)
            {
                ModStatus = "Index refreshed successfully";
                SetStatus("Index refreshed successfully");
                await RefreshAvailableModsAsync();
            }
            else
            {
                ModStatus = $"Failed to refresh index: {result.Error}";
                SetStatus($"Failed to refresh index: {result.Error}", true);
            }
        }, "Refreshing mod index...");
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            _ = SearchModsAsync(); // fire and forget
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            _ = SearchModsAsync(); // fire and forget
        }
    }

    [RelayCommand]
    private void SelectInstalledMod(ModInfo mod)
    {
        SelectedInstalledMod = mod;
        ModStatus = $"Selected installed mod: {mod.Name}";
    }

    [RelayCommand]
    private void SelectAvailableMod(ModIndexEntry mod)
    {
        SelectedAvailableMod = mod;
        ModStatus = $"Selected available mod: {mod.Name}";
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
                _snackbarService.Show(
                    "Success", 
                    $"Successfully installed {Path.GetFileName(filePath)}.", 
                    "Success", 
                    "✅", 
                    TimeSpan.FromSeconds(3));
                await RefreshModsAsync();
            }
            else
            {
                ModStatus = $"Installation failed: {result.Error}";
                SetStatus($"Installation failed: {result.Error}", true);
                _snackbarService.Show(
                    "Installation Failed", 
                    result.Error, 
                    "Error", 
                    "❌", 
                    TimeSpan.FromSeconds(5));
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
    private async Task UninstallModAsync(ModInfo? mod = null)
    {
        var targetMod = mod ?? SelectedInstalledMod;
        if (targetMod == null || GameInstallation == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Uninstall Mod",
            $"Are you sure you want to uninstall '{targetMod.Name}'? This action cannot be undone.");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = $"Uninstalling {targetMod.Name}...";
            
            var uninstallResult = await _modManager.UninstallMod(targetMod.Id, GameInstallation);
            
            if (uninstallResult.Success)
            {
                ModStatus = "Mod uninstalled successfully";
                SetStatus("Mod uninstalled successfully");
                _snackbarService.Show(
                    "Success", 
                    $"Successfully uninstalled {targetMod.Name}.", 
                    "Success", 
                    "✅", 
                    TimeSpan.FromSeconds(3));
                await RefreshModsAsync();
            }
            else
            {
                ModStatus = $"Uninstallation failed: {uninstallResult.Error}";
                SetStatus($"Uninstallation failed: {uninstallResult.Error}", true);
                _snackbarService.Show(
                    "Uninstallation Failed", 
                    uninstallResult.Error, 
                    "Error", 
                    "❌", 
                    TimeSpan.FromSeconds(5));
            }
        }, "Uninstalling mod...");
    }

    [RelayCommand(CanExecute = nameof(CanEnableMod))]
    private async Task EnableModAsync(ModInfo? mod = null)
    {
        var targetMod = mod ?? SelectedInstalledMod;
        if (targetMod == null || GameInstallation == null || targetMod.IsEnabled) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = $"Enabling {targetMod.Name}...";
            
            var result = await _modManager.EnableMod(targetMod.Id, GameInstallation);
            
            if (result.Success)
            {
                ModStatus = "Mod enabled successfully";
                SetStatus("Mod enabled successfully");
                _snackbarService.Show(
                    "Success", 
                    $"{targetMod.Name} has been enabled.", 
                    "Success", 
                    "✅", 
                    TimeSpan.FromSeconds(3));
                await RefreshModsAsync();
            }
            else
            {
                ModStatus = $"Failed to enable mod: {result.Error}";
                SetStatus($"Failed to enable mod: {result.Error}", true);
                _snackbarService.Show(
                    "Error", 
                    $"Failed to enable mod: {result.Error}", 
                    "Error", 
                    "❌", 
                    TimeSpan.FromSeconds(5));
            }
        }, "Enabling mod...");
    }

    [RelayCommand(CanExecute = nameof(CanDisableMod))]
    private async Task DisableModAsync(ModInfo? mod = null)
    {
        var targetMod = mod ?? SelectedInstalledMod;
        if (targetMod == null || GameInstallation == null || !targetMod.IsEnabled) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = $"Disabling {targetMod.Name}...";
            
            var result = await _modManager.DisableMod(targetMod.Id, GameInstallation);
            
            if (result.Success)
            {
                ModStatus = "Mod disabled successfully";
                SetStatus("Mod disabled successfully");
                _snackbarService.Show(
                    "Success", 
                    $"{targetMod.Name} has been disabled.", 
                    "Success", 
                    "✅", 
                    TimeSpan.FromSeconds(3));
                await RefreshModsAsync();
            }
            else
            {
                ModStatus = $"Failed to disable mod: {result.Error}";
                SetStatus($"Failed to disable mod: {result.Error}", true);
                _snackbarService.Show(
                    "Error", 
                    $"Failed to disable mod: {result.Error}", 
                    "Error", 
                    "❌", 
                    TimeSpan.FromSeconds(5));
            }
        }, "Disabling mod...");
    }

    [RelayCommand(CanExecute = nameof(CanToggleMod))]
    private async Task ToggleModAsync(ModInfo? mod = null)
    {
        var targetMod = mod ?? SelectedInstalledMod;
        if (targetMod == null || GameInstallation == null) return;

        if (targetMod.IsEnabled)
        {
            await DisableModAsync(targetMod);
        }
        else
        {
            await EnableModAsync(targetMod);
        }
    }

    private bool CanUninstallMod(ModInfo? mod)
    {
        // enable uninstall if the given mod (or selected mod) is installed
        var target = mod ?? SelectedInstalledMod;
        return target != null && target.IsInstalled;
    }

    private bool CanEnableMod(ModInfo? mod)
    {
        var target = mod ?? SelectedInstalledMod;
        return target != null && target.IsInstalled && !target.IsEnabled;
    }

    private bool CanDisableMod(ModInfo? mod)
    {
        var target = mod ?? SelectedInstalledMod;
        return target != null && target.IsInstalled && target.IsEnabled;
    }

    private bool CanToggleMod(ModInfo? mod)
    {
        var target = mod ?? SelectedInstalledMod;
        return target != null && target.IsInstalled;
    }

    // properties for XAML binding (fall back to selected mod when using toolbar buttons)
    public bool CanEnableModProperty => CanEnableMod(null);
    public bool CanDisableModProperty => CanDisableMod(null);
    public bool CanUninstallModProperty => CanUninstallMod(null);

    [RelayCommand(CanExecute = nameof(CanOpenModSettings))]
    private async Task OpenModSettingsAsync()
    {
        if (SelectedInstalledMod == null || GameInstallation == null) return;
        await _dialogService.ShowModSettingsAsync(GameInstallation, SelectedInstalledMod.Id);
    }

    private bool CanOpenModSettings()
    {
        return SelectedInstalledMod != null;
    }

    public bool CanOpenModSettingsProperty => CanOpenModSettings();

    [RelayCommand]
    private void FilterMods()
    {
        // trigger search when filters change
        _ = SearchModsAsync(); // fire and forget
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (GameInstallation == null) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Checking for updates...";
            UpdateStatus = "Checking for updates...";

            var updates = await _modUpdateService.CheckForUpdates(GameInstallation);

            AvailableUpdates.Clear();
            foreach (var update in updates)
            {
                AvailableUpdates.Add(update);
            }

            if (updates.Count > 0)
            {
                UpdateStatus = $"Found {updates.Count} update(s) available";
                ModStatus = $"Found {updates.Count} update(s) available";
            }
            else
            {
                UpdateStatus = "All mods are up to date";
                ModStatus = "All mods are up to date";
            }
        }, "Checking for updates...");
    }

    [RelayCommand]
    private async Task InstallAllUpdatesAsync()
    {
        if (AvailableUpdates.Count == 0 || GameInstallation == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Install All Updates",
            $"Are you sure you want to install {AvailableUpdates.Count} update(s)? This may take some time.");

        if (!result) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Installing updates...";
            var successCount = 0;
            var failureCount = 0;

            foreach (var update in AvailableUpdates)
            {
                try
                {
                    var updateResult = await _modUpdateService.InstallUpdate(update, GameInstallation);
                    if (updateResult.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failureCount++;
                        Logger.LogWarning("Failed to install update for {ModId}: {Error}", update.ModId, updateResult.Error);
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    Logger.LogError(ex, "Error installing update for {ModId}", update.ModId);
                }
            }

            if (failureCount == 0)
            {
                ModStatus = $"Successfully installed {successCount} update(s)";
                UpdateStatus = "All updates installed successfully";
                SetStatus($"Successfully installed {successCount} update(s)");
            }
            else
            {
                ModStatus = $"Installed {successCount} update(s), {failureCount} failed";
                UpdateStatus = $"{successCount} successful, {failureCount} failed";
                SetStatus($"Installed {successCount} update(s), {failureCount} failed", true);
            }

            await RefreshModsAsync();
            await CheckForUpdatesAsync();
        }, "Installing updates...");
    }

    [RelayCommand]
    private async Task DetectConflictsAsync()
    {
        if (GameInstallation == null) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Detecting conflicts...";
            ConflictStatus = "Detecting conflicts...";

            var conflicts = await _conflictService.DetectConflicts(GameInstallation);

            DetectedConflicts.Clear();
            foreach (var conflict in conflicts)
            {
                DetectedConflicts.Add(conflict);
            }

            if (conflicts.Count > 0)
            {
                var criticalCount = conflicts.Count(c => c.Severity == ConflictSeverity.Critical);
                var errorCount = conflicts.Count(c => c.Severity == ConflictSeverity.Error);
                var warningCount = conflicts.Count(c => c.Severity == ConflictSeverity.Warning);

                ConflictStatus = $"Found {conflicts.Count} conflict(s): {criticalCount} critical, {errorCount} errors, {warningCount} warnings";
                ModStatus = $"Found {conflicts.Count} conflict(s)";
            }
            else
            {
                ConflictStatus = "No conflicts detected";
                ModStatus = "No conflicts detected";
            }
        }, "Detecting conflicts...");
    }

    [RelayCommand]
    private async Task ResolveConflictAsync(ModConflict conflict)
    {
        if (GameInstallation == null || conflict == null) return;

        var resolutionOptions = await _conflictService.GetResolutionOptions(conflict);
        if (resolutionOptions.Count == 0)
        {
            await _dialogService.ShowMessageDialogAsync(
                "No Resolution Available",
                $"No automatic resolution is available for this conflict: {conflict.Description}");
            return;
        }

        // for now, try the first automatic resolution if available
        var autoResolution = resolutionOptions.FirstOrDefault(r => r.CanAutoResolve);
        if (autoResolution != null)
        {
            await ExecuteAsync(async () =>
            {
                ModStatus = "Resolving conflict...";
                
                var result = await _conflictService.ResolveConflict(conflict, GameInstallation);
                
                if (result.Success)
                {
                    ModStatus = "Conflict resolved successfully";
                    SetStatus("Conflict resolved successfully");
                    await DetectConflictsAsync(); // refresh conflicts
                }
                else
                {
                    ModStatus = $"Failed to resolve conflict: {result.Error}";
                    SetStatus($"Failed to resolve conflict: {result.Error}", true);
                }
            }, "Resolving conflict...");
        }
        else
        {
            await _dialogService.ShowMessageDialogAsync(
                "Manual Resolution Required",
                $"This conflict requires manual resolution: {conflict.Description}\n\nResolution: {resolutionOptions.First().Description}");
        }
    }

    [RelayCommand]
    private async Task CheckCompatibilityAsync()
    {
        if (GameInstallation == null) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Checking compatibility...";

            var modIds = InstalledMods.Select(m => m.Id).ToList();
            var compatibilities = await _compatibilityService.CheckCompatibility(modIds, GameInstallation);

            CompatibilityStatuses.Clear();
            foreach (var compatibility in compatibilities)
            {
                CompatibilityStatuses.Add(compatibility);
                
                // update installed mod meta
                var modInfo = InstalledMods.FirstOrDefault(m => m.Id == compatibility.ModId);
                if (modInfo != null)
                {
                    modInfo.CompatibilityStatus = compatibility.Status;
                    modInfo.VersionConstraints = compatibility.GameVersion;
                }
            }

            var incompatibleCount = compatibilities.Count(c => c.Status == CompatibilityStatus.Incompatible);
            var issuesCount = compatibilities.Count(c => c.Status == CompatibilityStatus.CompatibleWithIssues);

            if (incompatibleCount > 0 || issuesCount > 0)
            {
                ModStatus = $"Compatibility issues found: {incompatibleCount} incompatible, {issuesCount} with issues";
                SetStatus($"Compatibility issues found: {incompatibleCount} incompatible, {issuesCount} with issues", true);
            }
            else
            {
                ModStatus = "All mods are compatible";
                SetStatus("All mods are compatible");
            }
        }, "Checking compatibility...");
    }

    [RelayCommand]
    private async Task ConfigureAutoUpdatesAsync()
    {
        if (SelectedInstalledMod == null || GameInstallation == null) return;

        var settings = await _modUpdateService.GetUpdateSettings(SelectedInstalledMod.Id, GameInstallation);
        
        var enableAutoUpdate = await _dialogService.ShowConfirmationDialogAsync(
            "Configure Auto-Updates",
            $"Enable automatic updates for '{SelectedInstalledMod.Name}'?\n\nThis will automatically download and install updates when they become available.");

        if (enableAutoUpdate != settings.AutoUpdateEnabled)
        {
            settings.AutoUpdateEnabled = enableAutoUpdate;
            await _modUpdateService.SetUpdateSettings(settings, GameInstallation);

            if (enableAutoUpdate)
            {
                SetStatus($"Auto-updates enabled for {SelectedInstalledMod.Name}");
            }
            else
            {
                SetStatus($"Auto-updates disabled for {SelectedInstalledMod.Name}");
            }
        }
    }

    [RelayCommand]
    private async Task BackupModConfigsAsync()
    {
        if (SelectedInstalledMod == null || GameInstallation == null) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Backing up mod configurations...";

            var result = await _configService.BackupModConfigs(SelectedInstalledMod.Id, GameInstallation);

            if (result.Success)
            {
                ModStatus = "Mod configurations backed up successfully";
                SetStatus("Mod configurations backed up successfully");
            }
            else
            {
                ModStatus = $"Failed to backup configurations: {result.Error}";
                SetStatus($"Failed to backup configurations: {result.Error}", true);
            }
        }, "Backing up mod configurations...");
    }

    [RelayCommand]
    private async Task RestoreModConfigsAsync()
    {
        if (SelectedInstalledMod == null || GameInstallation == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Restore Mod Configurations",
            $"Are you sure you want to restore configurations for '{SelectedInstalledMod.Name}'? This will overwrite current settings.");

        if (!result) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Restoring mod configurations...";

            var restoreResult = await _configService.RestoreModConfigs(SelectedInstalledMod.Id, GameInstallation);

            if (restoreResult.Success)
            {
                ModStatus = "Mod configurations restored successfully";
                SetStatus("Mod configurations restored successfully");
            }
            else
            {
                ModStatus = $"Failed to restore configurations: {restoreResult.Error}";
                SetStatus($"Failed to restore configurations: {restoreResult.Error}", true);
            }
        }, "Restoring mod configurations...");
    }

    [RelayCommand]
    private async Task EnableAutoUpdateSystemAsync()
    {
        if (GameInstallation == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Enable Auto-Update System",
            "Enable the automatic update system? This will periodically check for and install mod updates in the background.");

        if (!result) return;

        await ExecuteAsync(async () =>
        {
            ModStatus = "Enabling auto-update system...";

            var scheduleResult = await _modUpdateService.ScheduleUpdateChecks(GameInstallation);

            if (scheduleResult.Success)
            {
                AutoUpdateEnabled = true;
                ModStatus = "Auto-update system enabled";
                SetStatus("Auto-update system enabled");
            }
            else
            {
                ModStatus = $"Failed to enable auto-updates: {scheduleResult.Error}";
                SetStatus($"Failed to enable auto-updates: {scheduleResult.Error}", true);
            }
        }, "Enabling auto-update system...");
    }

    [RelayCommand]
    private async Task DisableAutoUpdateSystemAsync()
    {
        await ExecuteAsync(async () =>
        {
            ModStatus = "Disabling auto-update system...";

            var cancelResult = await _modUpdateService.CancelUpdateChecks();

            if (cancelResult.Success)
            {
                AutoUpdateEnabled = false;
                ModStatus = "Auto-update system disabled";
                SetStatus("Auto-update system disabled");
            }
            else
            {
                ModStatus = $"Failed to disable auto-updates: {cancelResult.Error}";
                SetStatus($"Failed to disable auto-updates: {cancelResult.Error}", true);
            }
        }, "Disabling auto-update system...");
    }

    [RelayCommand]
    private void OpenModsFolder()
    {
        if (GameInstallation == null) return;
        try
        {
            var modsPath = System.IO.Path.Combine(GameInstallation.InstallPath, GameInstallation.ModDirectory);
            if (!System.IO.Directory.Exists(modsPath))
            {
                System.IO.Directory.CreateDirectory(modsPath);
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
        }
    }
}

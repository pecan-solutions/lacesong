using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lacesong.Core.Services;

/// <summary>
/// service for managing automatic mod updates
/// </summary>
public class ModUpdateService : IModUpdateService
{
    private readonly IModIndexService _modIndexService;
    private readonly ThunderstoreService _tsService;
    private readonly IModManager _modManager;
    private readonly IModConfigService _configService;
    private readonly IConflictDetectionService _conflictService;
    private readonly IVerificationService _verificationService;
    private readonly IBackupManager _backupManager;
    
    private Timer? _updateTimer;
    public event Action<List<ModUpdate>>? UpdatesAvailable;
    private readonly Dictionary<string, ModUpdateSettings> _updateSettings = new();
    private readonly object _settingsLock = new object();

    public ModUpdateService(
        IModIndexService modIndexService,
        ThunderstoreService tsService,
        IModManager modManager,
        IModConfigService configService,
        IConflictDetectionService conflictService,
        IVerificationService verificationService,
        IBackupManager backupManager)
    {
        _modIndexService = modIndexService;
        _tsService = tsService;
        _modManager = modManager;
        _configService = configService;
        _conflictService = conflictService;
        _verificationService = verificationService;
        _backupManager = backupManager;
    }

    public async Task<List<ModUpdate>> CheckForUpdates(GameInstallation gameInstall, List<string>? modIds = null, bool force = false)
    {
        var updates = new List<ModUpdate>();
        
        try
        {
            var installedMods = await _modManager.GetInstalledMods(gameInstall);
            var modsToCheck = modIds != null 
                ? installedMods.Where(m => modIds.Contains(m.Id)).ToList()
                : installedMods;

            foreach (var mod in modsToCheck)
            {
                var update = await CheckModForUpdate(mod, gameInstall, force);
                if (update != null)
                {
                    updates.Add(update);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking for updates: {ex.Message}");
        }

        return updates;
    }

    public async Task<OperationResult> InstallUpdate(ModUpdate update, GameInstallation gameInstall, ModUpdateSettings? options = null)
    {
        try
        {
            var settings = options ?? await GetUpdateSettings(update.ModId, gameInstall);
            
            // create backup if enabled
            if (settings.BackupBeforeUpdate)
            {
                var backupResult = await _backupManager.CreateBackup(gameInstall, $"update_{update.ModId}_{update.CurrentVersion}_to_{update.AvailableVersion}");
                if (!backupResult.Success)
                {
                    // log the backup failure but continue with update
                    Console.WriteLine($"Warning: Mod backup creation failed: {backupResult.Error}. Continuing with update without backup.");
                }
                else
                {
                    // store backup path for possible rollback
                    var backupPath = backupResult.Data as string;
                    settings.PendingBackupPath = backupPath;
                }
            }

            // backup configs if preservation is enabled
            List<ModConfig>? oldConfigs = null;
            string? configBackupPath = null;
            if (settings.PreserveConfigs)
            {
                var configBackupResult = await _configService.BackupModConfigs(update.ModId, gameInstall);
                if (!configBackupResult.Success)
                {
                    return OperationResult.ErrorResult($"Failed to backup configs: {configBackupResult.Error}", "Config backup failed");
                }
                configBackupPath = configBackupResult.Data as string;
                // read old config metadata for merge later
                if (!string.IsNullOrEmpty(configBackupPath))
                {
                    var manifestPath = Path.Combine(configBackupPath, "backup_manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        var manifestJson = await File.ReadAllTextAsync(manifestPath);
                        var manifest = JsonSerializer.Deserialize<JsonElement>(manifestJson);
                        oldConfigs = JsonSerializer.Deserialize<List<ModConfig>>(manifest.GetProperty("Configs"));
                    }
                }
            }

            // download and install the update
            var installResult = await _modManager.InstallModFromZip(update.DownloadUrl, gameInstall);
            if (!installResult.Success)
            {
                // restore configs on failure
                if (settings.PreserveConfigs)
                {
                    await _configService.RestoreModConfigs(update.ModId, gameInstall);
                }
                return OperationResult.ErrorResult($"Failed to install update: {installResult.Error}", "Update installation failed");
            }

            // verify installation
            var verificationResult = await VerifyUpdate(update, gameInstall);
            if (!verificationResult.Success)
            {
                // rollback if verification failed and backup exists
                if (settings.BackupBeforeUpdate && settings.PendingBackupPath != null)
                {
                    await _backupManager.RestoreBackup(settings.PendingBackupPath, gameInstall);
                }
                // restore configs from backup
                if (settings.PreserveConfigs && configBackupPath != null)
                {
                    await _configService.RestoreModConfigs(update.ModId, gameInstall, configBackupPath);
                }
                return OperationResult.ErrorResult($"Update verification failed: {verificationResult.Error}", "Update verification failed");
            }

            // merge configs if preservation enabled
            if (settings.PreserveConfigs && oldConfigs != null)
            {
                var newConfigs = await _configService.GetModConfigs(update.ModId, gameInstall);
                var mergeResult = await _configService.MergeConfigs(update.ModId, oldConfigs, newConfigs, gameInstall);
                if (!mergeResult.Success)
                {
                    Console.WriteLine($"Warning: failed to merge configs after update: {mergeResult.Error}");
                }
            }

            // update settings
            settings.LastUpdateCheck = DateTime.UtcNow;
            settings.PendingBackupPath = null;
            await SetUpdateSettings(settings, gameInstall);

            return OperationResult.SuccessResult($"Mod '{update.ModId}' updated successfully from {update.CurrentVersion} to {update.AvailableVersion}");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Update installation failed");
        }
    }

    public async Task<ModUpdateSettings> GetUpdateSettings(string modId, GameInstallation gameInstall)
    {
        lock (_settingsLock)
        {
            if (_updateSettings.TryGetValue(modId, out var settings))
            {
                return settings;
            }
        }

        // try to load from file
        var settingsPath = GetUpdateSettingsPath(gameInstall);
        if (File.Exists(settingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(settingsPath);
                var allSettings = JsonSerializer.Deserialize<Dictionary<string, ModUpdateSettings>>(json) ?? new Dictionary<string, ModUpdateSettings>();
                
                if (allSettings.TryGetValue(modId, out var loadedSettings))
                {
                    lock (_settingsLock)
                    {
                        _updateSettings[modId] = loadedSettings;
                    }
                    return loadedSettings;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading update settings: {ex.Message}");
            }
        }

        // return default settings
        var defaultSettings = new ModUpdateSettings
        {
            ModId = modId,
            AutoUpdateEnabled = false,
            UpdateChannel = "stable",
            UpdateFrequency = TimeSpan.FromDays(1),
            NotifyOnUpdates = true,
            BackupBeforeUpdate = true,
            PreserveConfigs = true
        };

        lock (_settingsLock)
        {
            _updateSettings[modId] = defaultSettings;
        }

        return defaultSettings;
    }

    public async Task<OperationResult> SetUpdateSettings(ModUpdateSettings settings, GameInstallation gameInstall)
    {
        try
        {
            lock (_settingsLock)
            {
                _updateSettings[settings.ModId] = settings;
            }

            // save to file
            var settingsPath = GetUpdateSettingsPath(gameInstall);
            var allSettings = new Dictionary<string, ModUpdateSettings>();
            
            if (File.Exists(settingsPath))
            {
                var json = await File.ReadAllTextAsync(settingsPath);
                allSettings = JsonSerializer.Deserialize<Dictionary<string, ModUpdateSettings>>(json) ?? new Dictionary<string, ModUpdateSettings>();
            }

            allSettings[settings.ModId] = settings;

            var updatedJson = JsonSerializer.Serialize(allSettings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(settingsPath, updatedJson);

            return OperationResult.SuccessResult("Update settings saved successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to save update settings");
        }
    }

    public async Task<OperationResult> ScheduleUpdateChecks(GameInstallation gameInstall)
    {
        try
        {
            // cancel existing timer
            await CancelUpdateChecks();

            // get installed mods with auto-update enabled
            var installedMods = await _modManager.GetInstalledMods(gameInstall);
            var autoUpdateMods = new List<string>();

            foreach (var mod in installedMods)
            {
                var settings = await GetUpdateSettings(mod.Id, gameInstall);
                if (settings.AutoUpdateEnabled)
                {
                    autoUpdateMods.Add(mod.Id);
                }
            }

            if (autoUpdateMods.Count == 0)
            {
                return OperationResult.SuccessResult("No mods have auto-update enabled");
            }

            // schedule periodic checks
            _updateTimer = new Timer(async _ => await PerformScheduledUpdateCheck(gameInstall, autoUpdateMods), 
                null, TimeSpan.Zero, TimeSpan.FromHours(6)); // check every 6 hours

            return OperationResult.SuccessResult($"Scheduled update checks for {autoUpdateMods.Count} mods");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to schedule update checks");
        }
    }

    public async Task<OperationResult> CancelUpdateChecks()
    {
        try
        {
            if (_updateTimer != null)
            {
                await _updateTimer.DisposeAsync();
                _updateTimer = null;
            }

            return OperationResult.SuccessResult("Update checks cancelled");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to cancel update checks");
        }
    }

    private async Task<ModUpdate?> CheckModForUpdate(ModInfo installedMod, GameInstallation gameInstall, bool force = false)
    {
        // parse id author-ModName maybe with version after? installedMod.Id is full name from thunderstore originally
        var parts = installedMod.Id.Split('-');
        if (parts.Length < 2)
        {
            return null; // cannot parse
        }
        var ns = parts[0];
        var pkgName = string.Join('-', parts.Skip(1));
        var detail = await _tsService.GetPackageDetailAsync(ns, pkgName, force);
        if (detail?.Latest == null)
        {
            return null;
        }
        var availableVersion = detail.Latest.Version_Number;
        if (ParseVersion(availableVersion) <= ParseVersion(installedMod.Version)) return null;
        return new ModUpdate
        {
            ModId = installedMod.Id,
            CurrentVersion = installedMod.Version,
            AvailableVersion = availableVersion,
            UpdateType = DetermineUpdateType(ParseVersion(installedMod.Version), ParseVersion(availableVersion)),
            DownloadUrl = detail.Latest.Download_Url,
            FileSize = 0,
            ReleaseDate = detail.Latest.Date_Created,
            IsPrerelease = false
        };
    }

    private async Task<OperationResult> VerifyUpdate(ModUpdate update, GameInstallation gameInstall)
    {
        try
        {
            // check if mod is properly installed
            var modInfo = await _modManager.GetModInfo(update.ModId, gameInstall);
            if (modInfo == null || modInfo.Version != update.AvailableVersion)
            {
                return OperationResult.ErrorResult("Mod version mismatch after update", "Update verification failed");
            }

            // check for conflicts
            var conflicts = await _conflictService.DetectConflicts(gameInstall);
            var modConflicts = conflicts.Where(c => c.ConflictingMods.Contains(update.ModId)).ToList();
            
            if (modConflicts.Any(c => c.Severity >= ConflictSeverity.Error))
            {
                return OperationResult.ErrorResult("Critical conflicts detected after update", "Update verification failed");
            }

            return OperationResult.SuccessResult("Update verification successful");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Update verification failed");
        }
    }

    private async Task PerformScheduledUpdateCheck(GameInstallation gameInstall, List<string> modIds)
    {
        try
        {
            var updates = await CheckForUpdates(gameInstall, modIds);
            
            if (updates.Count > 0)
            {
                UpdatesAvailable?.Invoke(updates);
            }

            foreach (var update in updates)
            {
                var settings = await GetUpdateSettings(update.ModId, gameInstall);
                
                // notify user if enabled
                if (settings.NotifyOnUpdates)
                {
                    Console.WriteLine($"Update available for {update.ModId}: {update.CurrentVersion} -> {update.AvailableVersion}");
                }

                // auto-install if enabled and not a breaking change
                if (settings.AutoUpdateEnabled && update.UpdateType != UpdateType.Breaking)
                {
                    await InstallUpdate(update, gameInstall, settings);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during scheduled update check: {ex.Message}");
        }
    }

    private string GetUpdateSettingsPath(GameInstallation gameInstall)
    {
        var settingsDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "config", "lacesong");
        Directory.CreateDirectory(settingsDir);
        return Path.Combine(settingsDir, "update_settings.json");
    }

    private string GetGameVersion(GameInstallation gameInstall)
    {
        // try to read game version from executable or version file
        var versionFile = Path.Combine(gameInstall.InstallPath, "version.txt");
        if (File.Exists(versionFile))
        {
            return File.ReadAllText(versionFile).Trim();
        }

        // fallback to detecting from executable
        var executablePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
        if (File.Exists(executablePath))
        {
            var fileInfo = FileVersionInfo.GetVersionInfo(executablePath);
            return fileInfo.FileVersion ?? "1.0.0";
        }

        return "1.0.0"; // default version
    }

    private bool IsVersionCompatible(ModVersion version, string gameVersion)
    {
        // check game version compatibility
        if (!string.IsNullOrEmpty(version.GameVersion) && version.GameVersion != gameVersion)
        {
            return false;
        }

        return true; // assume compatible if no specific requirements
    }

    private Version ParseVersion(string versionString)
    {
        try
        {
            // clean version string (remove any non-numeric characters except dots and dashes)
            var cleanVersion = Regex.Replace(versionString, @"[^\d\.\-]", "");
            
            // handle pre-release versions (e.g., "1.0.0-beta")
            if (cleanVersion.Contains('-'))
            {
                cleanVersion = cleanVersion.Split('-')[0];
            }

            return Version.Parse(cleanVersion);
        }
        catch
        {
            return new Version(0, 0, 0); // fallback for invalid versions
        }
    }

    private UpdateType DetermineUpdateType(Version currentVersion, Version availableVersion)
    {
        if (availableVersion.Major > currentVersion.Major)
        {
            return UpdateType.Major;
        }
        else if (availableVersion.Minor > currentVersion.Minor)
        {
            return UpdateType.Minor;
        }
        else if (availableVersion.Build > currentVersion.Build)
        {
            return UpdateType.Patch;
        }

        return UpdateType.Patch;
    }
}

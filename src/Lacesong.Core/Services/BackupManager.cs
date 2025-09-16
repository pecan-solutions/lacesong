using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.IO.Compression;
using System.Text.Json;

namespace Lacesong.Core.Services;

/// <summary>
/// service for managing backup and restore operations
/// </summary>
public class BackupManager : IBackupManager
{
    private const string BackupExtension = ".lcb"; // lacesong backup
    private const string BackupManifestFileName = "backup_manifest.json";

    public async Task<OperationResult> CreateBackup(GameInstallation gameInstall, string backupName)
    {
        try
        {
            // validate game installation
            if (!ValidateGameInstall(gameInstall))
            {
                return OperationResult.ErrorResult("Invalid game installation", "Game installation validation failed");
            }

            // create backup directory
            var backupDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "backups");
            Directory.CreateDirectory(backupDir);

            // generate backup filename
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"{backupName}_{timestamp}{BackupExtension}";
            var backupPath = Path.Combine(backupDir, backupFileName);

            // create temporary directory for backup contents
            var tempBackupDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempBackupDir);

            try
            {
                // backup bepinex configuration
                await BackupBepInExConfig(gameInstall, tempBackupDir);

                // backup installed mods
                await BackupInstalledMods(gameInstall, tempBackupDir);

                // backup mod list
                await BackupModList(gameInstall, tempBackupDir);

                // create backup manifest
                var manifest = new BackupManifest
                {
                    Name = backupName,
                    CreatedDate = DateTime.Now,
                    GameInstall = gameInstall,
                    Description = $"Backup created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    Version = "1.0.0"
                };

                var manifestPath = Path.Combine(tempBackupDir, BackupManifestFileName);
                var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(manifestPath, manifestJson);

                // create backup archive
                ZipFile.CreateFromDirectory(tempBackupDir, backupPath);

                // cleanup temp directory
                Directory.Delete(tempBackupDir, true);

                // create restore point object for return
                var restorePoint = new RestorePoint
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = backupName,
                    Description = $"Backup created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    Created = DateTime.UtcNow,
                    BackupPath = backupPath,
                    GameInstall = gameInstall,
                    Size = new FileInfo(backupPath).Length,
                    IsAutomatic = backupName.StartsWith("auto_"),
                    Tags = backupName.StartsWith("auto_") ? new List<string> { "automatic" } : new List<string>()
                };

                return OperationResult.SuccessResult($"Backup '{backupName}' created successfully", restorePoint);
            }
            catch
            {
                // cleanup temp directory on error
                if (Directory.Exists(tempBackupDir))
                {
                    Directory.Delete(tempBackupDir, true);
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Backup creation failed");
        }
    }

    public async Task<OperationResult> RestoreBackup(string backupPath, GameInstallation gameInstall)
    {
        try
        {
            // validate backup file
            if (!File.Exists(backupPath))
            {
                return OperationResult.ErrorResult("Backup file not found", "Backup file does not exist");
            }

            // validate game installation
            if (!ValidateGameInstall(gameInstall))
            {
                return OperationResult.ErrorResult("Invalid game installation", "Game installation validation failed");
            }

            // create temporary directory for extraction
            var tempRestoreDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRestoreDir);

            try
            {
                // extract backup
                ZipFile.ExtractToDirectory(backupPath, tempRestoreDir);

                // validate backup manifest
                var manifestPath = Path.Combine(tempRestoreDir, BackupManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    return OperationResult.ErrorResult("Invalid backup file", "Backup manifest not found");
                }

                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson);
                if (manifest == null)
                {
                    return OperationResult.ErrorResult("Invalid backup manifest", "Failed to parse backup manifest");
                }

                // create current backup before restoring
                var currentBackupResult = await CreateBackup(gameInstall, "pre_restore");
                if (!currentBackupResult.Success)
                {
                    return OperationResult.ErrorResult($"Failed to create pre-restore backup: {currentBackupResult.Error}", "Pre-restore backup failed");
                }

                // restore bepinex configuration
                await RestoreBepInExConfig(gameInstall, tempRestoreDir);

                // restore installed mods
                await RestoreInstalledMods(gameInstall, tempRestoreDir);

                // restore mod list
                await RestoreModList(gameInstall, tempRestoreDir);

                // cleanup temp directory
                Directory.Delete(tempRestoreDir, true);

                return OperationResult.SuccessResult($"Backup '{manifest.Name}' restored successfully");
            }
            catch
            {
                // cleanup temp directory on error
                if (Directory.Exists(tempRestoreDir))
                {
                    Directory.Delete(tempRestoreDir, true);
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Backup restore failed");
        }
    }

    public async Task<List<BackupInfo>> ListBackups(GameInstallation gameInstall)
    {
        var backups = new List<BackupInfo>();

        try
        {
            var backupDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "backups");
            if (!Directory.Exists(backupDir))
                return backups;

            var backupFiles = Directory.GetFiles(backupDir, $"*{BackupExtension}");
            foreach (var backupFile in backupFiles)
            {
                try
                {
                    var backupInfo = await GetBackupInfo(backupFile);
                    if (backupInfo != null)
                    {
                        backups.Add(backupInfo);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading backup info for {backupFile}: {ex.Message}");
                }
            }

            // sort by creation date (newest first)
            backups.Sort((a, b) => b.CreatedDate.CompareTo(a.CreatedDate));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing backups: {ex.Message}");
        }

        return backups;
    }

    public async Task<OperationResult> DeleteBackup(string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath))
            {
                return OperationResult.ErrorResult("Backup file not found", "Backup file does not exist");
            }

            File.Delete(backupPath);
            return OperationResult.SuccessResult("Backup deleted successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to delete backup");
        }
    }

    private bool ValidateGameInstall(GameInstallation gameInstall)
    {
        if (string.IsNullOrEmpty(gameInstall.InstallPath) || string.IsNullOrEmpty(gameInstall.Executable))
            return false;

        var executablePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
        return File.Exists(executablePath);
    }

    private async Task BackupBepInExConfig(GameInstallation gameInstall, string tempBackupDir)
    {
        try
        {
            var bepinexConfigDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "config");
            if (Directory.Exists(bepinexConfigDir))
            {
                var backupConfigDir = Path.Combine(tempBackupDir, "config");
                Directory.CreateDirectory(backupConfigDir);

                // copy config files
                var configFiles = Directory.GetFiles(bepinexConfigDir, "*", SearchOption.AllDirectories);
                foreach (var configFile in configFiles)
                {
                    var relativePath = Path.GetRelativePath(bepinexConfigDir, configFile);
                    var backupPath = Path.Combine(backupConfigDir, relativePath);
                    var backupDir = Path.GetDirectoryName(backupPath);
                    
                    if (!string.IsNullOrEmpty(backupDir))
                    {
                        Directory.CreateDirectory(backupDir);
                    }
                    
                    File.Copy(configFile, backupPath, true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error backing up BepInEx config: {ex.Message}");
        }
    }

    private async Task BackupInstalledMods(GameInstallation gameInstall, string tempBackupDir)
    {
        try
        {
            var modsDir = Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory);
            if (Directory.Exists(modsDir))
            {
                var backupModsDir = Path.Combine(tempBackupDir, "mods");
                Directory.CreateDirectory(backupModsDir);

                // copy mod directories
                var modDirs = Directory.GetDirectories(modsDir);
                foreach (var modDir in modDirs)
                {
                    var modName = Path.GetFileName(modDir);
                    var backupModDir = Path.Combine(backupModsDir, modName);
                    Directory.CreateDirectory(backupModDir);

                    // copy mod files
                    var modFiles = Directory.GetFiles(modDir, "*", SearchOption.AllDirectories);
                    foreach (var modFile in modFiles)
                    {
                        var relativePath = Path.GetRelativePath(modDir, modFile);
                        var backupPath = Path.Combine(backupModDir, relativePath);
                        var backupDir = Path.GetDirectoryName(backupPath);
                        
                        if (!string.IsNullOrEmpty(backupDir))
                        {
                            Directory.CreateDirectory(backupDir);
                        }
                        
                        File.Copy(modFile, backupPath, true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error backing up installed mods: {ex.Message}");
        }
    }

    private async Task BackupModList(GameInstallation gameInstall, string tempBackupDir)
    {
        try
        {
            var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
            if (File.Exists(modsListPath))
            {
                var backupModsListPath = Path.Combine(tempBackupDir, "mods_list.json");
                File.Copy(modsListPath, backupModsListPath, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error backing up mod list: {ex.Message}");
        }
    }

    private async Task RestoreBepInExConfig(GameInstallation gameInstall, string tempRestoreDir)
    {
        try
        {
            var backupConfigDir = Path.Combine(tempRestoreDir, "config");
            if (Directory.Exists(backupConfigDir))
            {
                var bepinexConfigDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "config");
                Directory.CreateDirectory(bepinexConfigDir);

                // copy config files back
                var configFiles = Directory.GetFiles(backupConfigDir, "*", SearchOption.AllDirectories);
                foreach (var configFile in configFiles)
                {
                    var relativePath = Path.GetRelativePath(backupConfigDir, configFile);
                    var restorePath = Path.Combine(bepinexConfigDir, relativePath);
                    var restoreDir = Path.GetDirectoryName(restorePath);
                    
                    if (!string.IsNullOrEmpty(restoreDir))
                    {
                        Directory.CreateDirectory(restoreDir);
                    }
                    
                    File.Copy(configFile, restorePath, true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error restoring BepInEx config: {ex.Message}");
        }
    }

    private async Task RestoreInstalledMods(GameInstallation gameInstall, string tempRestoreDir)
    {
        try
        {
            var backupModsDir = Path.Combine(tempRestoreDir, "mods");
            if (Directory.Exists(backupModsDir))
            {
                var modsDir = Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory);
                Directory.CreateDirectory(modsDir);

                // clear existing mods
                if (Directory.Exists(modsDir))
                {
                    var existingMods = Directory.GetDirectories(modsDir);
                    foreach (var existingMod in existingMods)
                    {
                        Directory.Delete(existingMod, true);
                    }
                }

                // restore mod directories
                var modDirs = Directory.GetDirectories(backupModsDir);
                foreach (var modDir in modDirs)
                {
                    var modName = Path.GetFileName(modDir);
                    var restoreModDir = Path.Combine(modsDir, modName);
                    Directory.CreateDirectory(restoreModDir);

                    // copy mod files back
                    var modFiles = Directory.GetFiles(modDir, "*", SearchOption.AllDirectories);
                    foreach (var modFile in modFiles)
                    {
                        var relativePath = Path.GetRelativePath(modDir, modFile);
                        var restorePath = Path.Combine(restoreModDir, relativePath);
                        var restoreDir = Path.GetDirectoryName(restorePath);
                        
                        if (!string.IsNullOrEmpty(restoreDir))
                        {
                            Directory.CreateDirectory(restoreDir);
                        }
                        
                        File.Copy(modFile, restorePath, true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error restoring installed mods: {ex.Message}");
        }
    }

    private async Task RestoreModList(GameInstallation gameInstall, string tempRestoreDir)
    {
        try
        {
            var backupModsListPath = Path.Combine(tempRestoreDir, "mods_list.json");
            if (File.Exists(backupModsListPath))
            {
                var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
                File.Copy(backupModsListPath, modsListPath, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error restoring mod list: {ex.Message}");
        }
    }

    private async Task<BackupInfo?> GetBackupInfo(string backupPath)
    {
        try
        {
            var fileInfo = new FileInfo(backupPath);
            var tempExtractDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            
            try
            {
                // extract just the manifest
                using var archive = ZipFile.OpenRead(backupPath);
                var manifestEntry = archive.Entries.FirstOrDefault(e => e.Name == BackupManifestFileName);
                
                if (manifestEntry != null)
                {
                    manifestEntry.ExtractToFile(Path.Combine(tempExtractDir, BackupManifestFileName), true);
                    
                    var manifestPath = Path.Combine(tempExtractDir, BackupManifestFileName);
                    var manifestJson = await File.ReadAllTextAsync(manifestPath);
                    var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson);
                    
                    if (manifest != null)
                    {
                        return new BackupInfo
                        {
                            Name = manifest.Name,
                            Path = backupPath,
                            CreatedDate = manifest.CreatedDate,
                            SizeBytes = fileInfo.Length,
                            Description = manifest.Description
                        };
                    }
                }
            }
            finally
            {
                // cleanup temp directory
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, true);
                }
            }

            // fallback: create basic backup info from file
            return new BackupInfo
            {
                Name = Path.GetFileNameWithoutExtension(backupPath),
                Path = backupPath,
                CreatedDate = fileInfo.CreationTime,
                SizeBytes = fileInfo.Length,
                Description = "Backup information not available"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting backup info for {backupPath}: {ex.Message}");
            return null;
        }
    }

    public async Task<OperationResult> CreateRestorePoint(GameInstallation gameInstall, string name, string? description = null, List<string>? tags = null)
    {
        try
        {
            // validate game installation
            if (!ValidateGameInstall(gameInstall))
            {
                return OperationResult.ErrorResult("Invalid game installation", "Game installation validation failed");
            }

            // create restore point directory
            var restorePointDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "restore_points");
            Directory.CreateDirectory(restorePointDir);

            // generate restore point filename
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var restorePointFileName = $"{name}_{timestamp}{BackupExtension}";
            var restorePointPath = Path.Combine(restorePointDir, restorePointFileName);

            // get current mod state
            var installedMods = await GetInstalledModsForBackup(gameInstall);
            var bepinexVersion = GetBepInExVersionForBackup(gameInstall);

            // create restore point
            var restorePoint = new RestorePoint
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description ?? $"Restore point created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                Created = DateTime.UtcNow,
                BackupPath = restorePointPath,
                GameInstall = gameInstall,
                Mods = installedMods,
                BepInExVersion = bepinexVersion,
                Size = 0, // will be calculated after creation
                IsAutomatic = false,
                Tags = tags ?? new List<string>()
            };

            // create backup archive
            var tempBackupDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempBackupDir);

            try
            {
                // backup current state
                await BackupBepInExConfig(gameInstall, tempBackupDir);
                await BackupInstalledMods(gameInstall, tempBackupDir);
                await BackupModList(gameInstall, tempBackupDir);

                // save restore point manifest
                var manifestPath = Path.Combine(tempBackupDir, "restore_point_manifest.json");
                var manifestJson = JsonSerializer.Serialize(restorePoint, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(manifestPath, manifestJson);

                // create archive
                ZipFile.CreateFromDirectory(tempBackupDir, restorePointPath);

                // calculate size
                var fileInfo = new FileInfo(restorePointPath);
                restorePoint.Size = fileInfo.Length;

                // cleanup temp directory
                Directory.Delete(tempBackupDir, true);

                return OperationResult.SuccessResult($"Restore point '{name}' created successfully", restorePoint);
            }
            catch
            {
                // cleanup temp directory on error
                if (Directory.Exists(tempBackupDir))
                {
                    Directory.Delete(tempBackupDir, true);
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Restore point creation failed");
        }
    }

    public async Task<List<RestorePoint>> ListRestorePoints(GameInstallation gameInstall)
    {
        var restorePoints = new List<RestorePoint>();

        try
        {
            var restorePointDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "restore_points");
            if (!Directory.Exists(restorePointDir))
            {
                return restorePoints;
            }

            var backupFiles = Directory.GetFiles(restorePointDir, $"*{BackupExtension}");

            foreach (var backupFile in backupFiles)
            {
                try
                {
                    var restorePoint = await LoadRestorePointFromFile(backupFile);
                    if (restorePoint != null)
                    {
                        restorePoints.Add(restorePoint);
                    }
                }
                catch
                {
                    // ignore corrupted restore points
                    continue;
                }
            }

            // sort by creation date (newest first)
            return restorePoints.OrderByDescending(rp => rp.Created).ToList();
        }
        catch
        {
            return restorePoints;
        }
    }

    public async Task<OperationResult> RestoreFromRestorePoint(string restorePointPath, GameInstallation gameInstall)
    {
        try
        {
            if (!File.Exists(restorePointPath))
            {
                return OperationResult.ErrorResult("Restore point file not found", "Restore point does not exist");
            }

            // validate game installation
            if (!ValidateGameInstall(gameInstall))
            {
                return OperationResult.ErrorResult("Invalid game installation", "Game installation validation failed");
            }

            // load restore point
            var restorePoint = await LoadRestorePointFromFile(restorePointPath);
            if (restorePoint == null)
            {
                return OperationResult.ErrorResult("Invalid restore point file", "Failed to load restore point");
            }

            // create backup before restore
            var currentBackupResult = await CreateBackup(gameInstall, "pre_restore");
            if (!currentBackupResult.Success)
            {
                return OperationResult.ErrorResult($"Failed to create pre-restore backup: {currentBackupResult.Error}", "Pre-restore backup failed");
            }

            // extract restore point
            var tempRestoreDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRestoreDir);

            try
            {
                ZipFile.ExtractToDirectory(restorePointPath, tempRestoreDir);

                // restore bepinex configuration
                await RestoreBepInExConfig(gameInstall, tempRestoreDir);

                // restore installed mods
                await RestoreInstalledMods(gameInstall, tempRestoreDir);

                // restore mod list
                await RestoreModList(gameInstall, tempRestoreDir);

                // cleanup temp directory
                Directory.Delete(tempRestoreDir, true);

                return OperationResult.SuccessResult($"Restore point '{restorePoint.Name}' restored successfully");
            }
            catch
            {
                // cleanup temp directory on error
                if (Directory.Exists(tempRestoreDir))
                {
                    Directory.Delete(tempRestoreDir, true);
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Restore point restoration failed");
        }
    }

    public async Task<OperationResult> CreateAutomaticRestorePoint(GameInstallation gameInstall, string operation)
    {
        try
        {
            var name = $"auto_{operation}_{DateTime.Now:yyyyMMdd_HHmmss}";
            var description = $"Automatic restore point before {operation} operation";
            var tags = new List<string> { "automatic", operation };

            return await CreateRestorePoint(gameInstall, name, description, tags);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Automatic restore point creation failed");
        }
    }

    private async Task<RestorePoint?> LoadRestorePointFromFile(string filePath)
    {
        try
        {
            var tempExtractDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempExtractDir);

            try
            {
                ZipFile.ExtractToDirectory(filePath, tempExtractDir);
                
                var manifestPath = Path.Combine(tempExtractDir, "restore_point_manifest.json");
                if (!File.Exists(manifestPath))
                {
                    return null;
                }

                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var restorePoint = JsonSerializer.Deserialize<RestorePoint>(manifestJson);
                
                // update file size from actual file
                var fileInfo = new FileInfo(filePath);
                if (restorePoint != null)
                {
                    restorePoint.Size = fileInfo.Length;
                    restorePoint.BackupPath = filePath;
                }

                return restorePoint;
            }
            finally
            {
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, true);
                }
            }
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<ModInfo>> GetInstalledModsForBackup(GameInstallation gameInstall)
    {
        try
        {
            var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
            if (!File.Exists(modsListPath))
            {
                return new List<ModInfo>();
            }

            var modsListJson = await File.ReadAllTextAsync(modsListPath);
            return JsonSerializer.Deserialize<List<ModInfo>>(modsListJson) ?? new List<ModInfo>();
        }
        catch
        {
            return new List<ModInfo>();
        }
    }

    private string? GetBepInExVersionForBackup(GameInstallation gameInstall)
    {
        try
        {
            var coreDllPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "core", "BepInEx.Core.dll");
            if (!File.Exists(coreDllPath))
            {
                return null;
            }

            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(coreDllPath);
            return versionInfo.FileVersion;
        }
        catch
        {
            return null;
        }
    }

    private class BackupManifest
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public GameInstallation GameInstall { get; set; } = new();
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }
}

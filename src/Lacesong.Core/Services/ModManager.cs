using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.IO.Compression;
using System.Text.Json;

namespace Lacesong.Core.Services;

/// <summary>
/// service for managing mod installation, uninstallation, and configuration
/// </summary>
public class ModManager : IModManager
{
    private readonly IBepInExManager _bepInExManager;
    private const string ModManifestFileName = "manifest.json";
    private const string ModInfoFileName = "modinfo.json";

    public ModManager(IBepInExManager bepInExManager)
    {
        _bepInExManager = bepInExManager;
    }

    public async Task<OperationResult> InstallModFromZip(string source, GameInstallation gameInstall)
    {
        try
        {
            // validate game installation
            if (!ValidateGameInstall(gameInstall))
            {
                return OperationResult.ErrorResult("Invalid game installation", "Game installation validation failed");
            }

            // check if bepinex is installed
            if (!_bepInExManager.IsBepInExInstalled(gameInstall))
            {
                return OperationResult.ErrorResult("BepInEx is not installed", "BepInEx required for mod installation");
            }

            string tempZipPath;
            
            // handle url downloads
            if (source.StartsWith("http://") || source.StartsWith("https://"))
            {
                var downloadResult = await DownloadMod(source);
                if (!downloadResult.Success)
                {
                    return OperationResult.ErrorResult($"Failed to download mod: {downloadResult.Error}", "Download failed");
                }
                tempZipPath = downloadResult.Data as string ?? string.Empty;
            }
            else
            {
                // validate local file
                if (!File.Exists(source))
                {
                    return OperationResult.ErrorResult("Mod file not found", "File does not exist");
                }
                tempZipPath = source;
            }

            // extract and analyze mod
            var extractResult = await ExtractAndAnalyzeMod(tempZipPath, gameInstall);
            if (!extractResult.Success)
            {
                return OperationResult.ErrorResult($"Failed to extract mod: {extractResult.Error}", "Extraction failed");
            }

            var modInfo = extractResult.Data as ModInfo;
            if (modInfo == null)
            {
                return OperationResult.ErrorResult("Failed to parse mod information", "Invalid mod format");
            }

            // check for dependencies
            var dependencyResult = await ResolveDependencies(modInfo, gameInstall);
            if (!dependencyResult.Success)
            {
                return OperationResult.ErrorResult($"Dependency resolution failed: {dependencyResult.Error}", "Dependencies not met");
            }

            // install mod files
            var installResult = await InstallModFiles(tempZipPath, modInfo, gameInstall);
            if (!installResult.Success)
            {
                return OperationResult.ErrorResult($"Failed to install mod files: {installResult.Error}", "Installation failed");
            }

            // cleanup temp files if downloaded
            if (source.StartsWith("http://") || source.StartsWith("https://"))
            {
                try
                {
                    File.Delete(tempZipPath);
                }
                catch
                {
                    // ignore cleanup errors
                }
            }

            return OperationResult.SuccessResult($"Mod '{modInfo.Name}' installed successfully", modInfo);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Mod installation failed");
        }
    }

    public async Task<OperationResult> UninstallMod(string modId, GameInstallation gameInstall)
    {
        try
        {
            var modInfo = await GetModInfo(modId, gameInstall);
            if (modInfo == null)
            {
                return OperationResult.ErrorResult("Mod not found", "Mod does not exist");
            }

            // create backup before uninstalling
            var backupResult = await CreateModBackup(modInfo, gameInstall);
            if (!backupResult.Success)
            {
                return OperationResult.ErrorResult($"Failed to create backup: {backupResult.Error}", "Backup creation failed");
            }

            // remove mod files
            var modPath = Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory, modId);
            if (Directory.Exists(modPath))
            {
                Directory.Delete(modPath, true);
            }

            // remove from mod list
            await RemoveModFromList(modId, gameInstall);

            return OperationResult.SuccessResult($"Mod '{modInfo.Name}' uninstalled successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Mod uninstallation failed");
        }
    }

    public async Task<OperationResult> EnableMod(string modId, GameInstallation gameInstall)
    {
        try
        {
            var modInfo = await GetModInfo(modId, gameInstall);
            if (modInfo == null)
            {
                return OperationResult.ErrorResult("Mod not found", "Mod does not exist");
            }

            if (modInfo.IsEnabled)
            {
                return OperationResult.SuccessResult("Mod is already enabled");
            }

            // enable mod by renaming .disabled files back to .dll
            var modPath = Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory, modId);
            if (Directory.Exists(modPath))
            {
                var disabledFiles = Directory.GetFiles(modPath, "*.disabled", SearchOption.AllDirectories);
                foreach (var disabledFile in disabledFiles)
                {
                    var enabledFile = Path.ChangeExtension(disabledFile, ".dll");
                    File.Move(disabledFile, enabledFile);
                }
            }

            // update mod status
            await UpdateModStatus(modId, true, gameInstall);

            return OperationResult.SuccessResult($"Mod '{modInfo.Name}' enabled successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to enable mod");
        }
    }

    public async Task<OperationResult> DisableMod(string modId, GameInstallation gameInstall)
    {
        try
        {
            var modInfo = await GetModInfo(modId, gameInstall);
            if (modInfo == null)
            {
                return OperationResult.ErrorResult("Mod not found", "Mod does not exist");
            }

            if (!modInfo.IsEnabled)
            {
                return OperationResult.SuccessResult("Mod is already disabled");
            }

            // disable mod by renaming .dll files to .disabled
            var modPath = Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory, modId);
            if (Directory.Exists(modPath))
            {
                var dllFiles = Directory.GetFiles(modPath, "*.dll", SearchOption.AllDirectories);
                foreach (var dllFile in dllFiles)
                {
                    var disabledFile = Path.ChangeExtension(dllFile, ".disabled");
                    File.Move(dllFile, disabledFile);
                }
            }

            // update mod status
            await UpdateModStatus(modId, false, gameInstall);

            return OperationResult.SuccessResult($"Mod '{modInfo.Name}' disabled successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to disable mod");
        }
    }

    public async Task<List<ModInfo>> GetInstalledMods(GameInstallation gameInstall)
    {
        var mods = new List<ModInfo>();

        try
        {
            var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
            if (File.Exists(modsListPath))
            {
                var json = await File.ReadAllTextAsync(modsListPath);
                mods = JsonSerializer.Deserialize<List<ModInfo>>(json) ?? new List<ModInfo>();
            }
            else
            {
                // scan mod directory for installed mods
                var modDirectory = Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory);
                if (Directory.Exists(modDirectory))
                {
                    var modDirs = Directory.GetDirectories(modDirectory);
                    foreach (var modDir in modDirs)
                    {
                        var modId = Path.GetFileName(modDir);
                        var modInfo = await GetModInfo(modId, gameInstall);
                        if (modInfo != null)
                        {
                            mods.Add(modInfo);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting installed mods: {ex.Message}");
        }

        return mods;
    }

    public async Task<ModInfo?> GetModInfo(string modId, GameInstallation gameInstall)
    {
        try
        {
            var modPath = Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory, modId);
            if (!Directory.Exists(modPath))
                return null;

            // look for mod info file
            var modInfoPath = Path.Combine(modPath, ModInfoFileName);
            if (File.Exists(modInfoPath))
            {
                var json = await File.ReadAllTextAsync(modInfoPath);
                var modInfo = JsonSerializer.Deserialize<ModInfo>(json);
                if (modInfo != null)
                {
                    // check if mod is enabled by looking for .dll files
                    var dllFiles = Directory.GetFiles(modPath, "*.dll", SearchOption.AllDirectories);
                    modInfo.IsEnabled = dllFiles.Length > 0;
                    modInfo.IsInstalled = true;
                    return modInfo;
                }
            }

            // fallback: create mod info from directory structure
            return new ModInfo
            {
                Id = modId,
                Name = modId,
                Version = "Unknown",
                Description = "No mod information available",
                Author = "Unknown",
                IsInstalled = true,
                IsEnabled = Directory.GetFiles(modPath, "*.dll", SearchOption.AllDirectories).Length > 0
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting mod info for {modId}: {ex.Message}");
            return null;
        }
    }

    private bool ValidateGameInstall(GameInstallation gameInstall)
    {
        if (string.IsNullOrEmpty(gameInstall.InstallPath) || string.IsNullOrEmpty(gameInstall.Executable))
            return false;

        var executablePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
        return File.Exists(executablePath);
    }


    private async Task<OperationResult> DownloadMod(string url)
    {
        try
        {
            var tempPath = Path.GetTempFileName();
            var tempZipPath = Path.ChangeExtension(tempPath, ".zip");
            File.Delete(tempPath);

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(tempZipPath, content);

            return OperationResult.SuccessResult("Mod downloaded successfully", tempZipPath);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to download mod");
        }
    }

    private async Task<OperationResult> ExtractAndAnalyzeMod(string zipPath, GameInstallation gameInstall)
    {
        try
        {
            var tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempExtractPath);

            // extract mod to temp directory
            ZipFile.ExtractToDirectory(zipPath, tempExtractPath);

            // look for mod manifest
            var manifestPath = Path.Combine(tempExtractPath, ModManifestFileName);
            ModInfo? modInfo = null;

            if (File.Exists(manifestPath))
            {
                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                modInfo = JsonSerializer.Deserialize<ModInfo>(manifestJson);
            }

            // if no manifest, try to infer from directory structure
            if (modInfo == null)
            {
                modInfo = await InferModInfo(tempExtractPath);
            }

            if (modInfo == null)
            {
                Directory.Delete(tempExtractPath, true);
                return OperationResult.ErrorResult("Could not determine mod information", "Invalid mod format");
            }

            // cleanup temp directory
            Directory.Delete(tempExtractPath, true);

            return OperationResult.SuccessResult("Mod analyzed successfully", modInfo);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to analyze mod");
        }
    }

    private async Task<ModInfo?> InferModInfo(string extractPath)
    {
        try
        {
            // look for dll files to determine mod structure
            var dllFiles = Directory.GetFiles(extractPath, "*.dll", SearchOption.AllDirectories);
            if (dllFiles.Length == 0)
                return null;

            // use the first dll file name as mod name
            var firstDll = dllFiles[0];
            var modName = Path.GetFileNameWithoutExtension(firstDll);

            return new ModInfo
            {
                Id = modName.ToLowerInvariant().Replace(" ", "-"),
                Name = modName,
                Version = "1.0.0",
                Description = "Mod installed from zip file",
                Author = "Unknown",
                Dependencies = new List<string>()
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<OperationResult> ResolveDependencies(ModInfo modInfo, GameInstallation gameInstall)
    {
        try
        {
            if (modInfo.Dependencies.Count == 0)
                return OperationResult.SuccessResult("No dependencies to resolve");

            var installedMods = await GetInstalledMods(gameInstall);
            var missingDependencies = new List<string>();

            foreach (var dependency in modInfo.Dependencies)
            {
                if (!installedMods.Any(m => m.Id.Equals(dependency, StringComparison.OrdinalIgnoreCase)))
                {
                    missingDependencies.Add(dependency);
                }
            }

            if (missingDependencies.Count > 0)
            {
                return OperationResult.ErrorResult($"Missing dependencies: {string.Join(", ", missingDependencies)}", "Dependencies not met");
            }

            return OperationResult.SuccessResult("All dependencies resolved");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to resolve dependencies");
        }
    }

    private async Task<OperationResult> InstallModFiles(string zipPath, ModInfo modInfo, GameInstallation gameInstall)
    {
        try
        {
            var modPath = Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory, modInfo.Id);
            
            // create mod directory
            Directory.CreateDirectory(modPath);

            // extract mod files
            ZipFile.ExtractToDirectory(zipPath, modPath);

            // save mod info
            var modInfoPath = Path.Combine(modPath, ModInfoFileName);
            var modInfoJson = JsonSerializer.Serialize(modInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(modInfoPath, modInfoJson);

            // add to mods list
            await AddModToList(modInfo, gameInstall);

            return OperationResult.SuccessResult("Mod files installed successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to install mod files");
        }
    }

    private async Task AddModToList(ModInfo modInfo, GameInstallation gameInstall)
    {
        try
        {
            var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
            var mods = new List<ModInfo>();

            if (File.Exists(modsListPath))
            {
                var json = await File.ReadAllTextAsync(modsListPath);
                mods = JsonSerializer.Deserialize<List<ModInfo>>(json) ?? new List<ModInfo>();
            }

            // remove existing entry if present
            mods.RemoveAll(m => m.Id == modInfo.Id);
            
            // add new entry
            mods.Add(modInfo);

            // save updated list
            var updatedJson = JsonSerializer.Serialize(mods, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(modsListPath, updatedJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding mod to list: {ex.Message}");
        }
    }

    private async Task RemoveModFromList(string modId, GameInstallation gameInstall)
    {
        try
        {
            var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
            if (!File.Exists(modsListPath))
                return;

            var json = await File.ReadAllTextAsync(modsListPath);
            var mods = JsonSerializer.Deserialize<List<ModInfo>>(json) ?? new List<ModInfo>();

            // remove mod from list
            mods.RemoveAll(m => m.Id == modId);

            // save updated list
            var updatedJson = JsonSerializer.Serialize(mods, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(modsListPath, updatedJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing mod from list: {ex.Message}");
        }
    }

    private async Task UpdateModStatus(string modId, bool isEnabled, GameInstallation gameInstall)
    {
        try
        {
            var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
            if (!File.Exists(modsListPath))
                return;

            var json = await File.ReadAllTextAsync(modsListPath);
            var mods = JsonSerializer.Deserialize<List<ModInfo>>(json) ?? new List<ModInfo>();

            // update mod status
            var mod = mods.FirstOrDefault(m => m.Id == modId);
            if (mod != null)
            {
                mod.IsEnabled = isEnabled;
            }

            // save updated list
            var updatedJson = JsonSerializer.Serialize(mods, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(modsListPath, updatedJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating mod status: {ex.Message}");
        }
    }

    private async Task<OperationResult> CreateModBackup(ModInfo modInfo, GameInstallation gameInstall)
    {
        try
        {
            var backupDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "backups", "mods");
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupDir, $"{modInfo.Id}_backup_{timestamp}.zip");

            var modPath = Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory, modInfo.Id);
            if (Directory.Exists(modPath))
            {
                ZipFile.CreateFromDirectory(modPath, backupPath);
            }

            return OperationResult.SuccessResult("Mod backup created successfully", backupPath);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to create mod backup");
        }
    }
}

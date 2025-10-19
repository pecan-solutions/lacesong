using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.IO.Compression;
using System.Text.Json;
using System.Linq;

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
        => await InstallModFromZip(source, gameInstall, progress: null, CancellationToken.None);

    public async Task<OperationResult> InstallModFromZip(string source, GameInstallation gameInstall, IProgress<double>? progress, CancellationToken token = default)
    {
        return await InstallModFromZip(source, gameInstall, progress, token, null);
    }

    public async Task<OperationResult> InstallModFromZip(string source, GameInstallation gameInstall, IProgress<double>? progress, CancellationToken token = default, string? owner = null)
    {
        Console.WriteLine($"ModManager: InstallModFromZip called with source: {source}");
        Console.WriteLine($"ModManager: GameInstall - Name: {gameInstall.Name}, Path: {gameInstall.InstallPath}");
        Console.WriteLine($"ModManager: Owner from API: {owner ?? "null"}");
        
        try
        {
            string tempZipPath = string.Empty;
            bool deleteTemp = false;
            try
            {
                // ---------------- download or reference source zip ----------------
                if (source.StartsWith("http://") || source.StartsWith("https://"))
                {
                    Console.WriteLine("ModManager: Downloading mod from URL");
                    var downloadResult = await DownloadModWithProgress(source, progress, token);
                    if (!downloadResult.Success)
                    {
                        Console.WriteLine($"ModManager: Download failed: {downloadResult.Error}");
                        return downloadResult;
                    }
                    tempZipPath = downloadResult.Data as string ?? string.Empty;
                    deleteTemp = true;
                    Console.WriteLine($"ModManager: Download successful, temp path: {tempZipPath}");
                }
                else
                {
                    Console.WriteLine($"ModManager: Using local file: {source}");
                    if (!File.Exists(source))
                    {
                        Console.WriteLine("ModManager: Local file does not exist");
                        return OperationResult.ErrorResult("Mod file not found", "File does not exist");
                    }
                    tempZipPath = source;
                }

                progress?.Report(0.4);
                Console.WriteLine("ModManager: Progress at 40% - starting extraction");

                // ---------------- extract and analyze ----------------
                Console.WriteLine("ModManager: Extracting and analyzing mod");
                var extractResult = await ExtractAndAnalyzeMod(tempZipPath, gameInstall, owner);
                if (!extractResult.Success)
                {
                    Console.WriteLine($"ModManager: Extract and analyze failed: {extractResult.Error}");
                    return extractResult;
                }
                progress?.Report(0.6);
                Console.WriteLine("ModManager: Progress at 60% - extraction complete");

                var modInfo = extractResult.Data as ModInfo ?? throw new("failed to parse mod info");
                Console.WriteLine($"ModManager: Mod info parsed - Name: {modInfo.Name}, ID: {modInfo.Id}, Author: {modInfo.Author}");

                // ---------------- handle reinstall ----------------
                Console.WriteLine("ModManager: Checking for existing installation on disk to allow reinstall");
                var modsBasePath = GetModsDirectoryPath(gameInstall);
                var existingModPath = Path.Combine(modsBasePath, GetFolderName(modInfo));
                if (Directory.Exists(existingModPath))
                {
                    Console.WriteLine($"ModManager: Existing mod directory found at {existingModPath}, deleting for reinstall");
                    try
                    {
                        Directory.Delete(existingModPath, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ModManager: Failed to delete existing mod directory: {ex.Message}");
                        return OperationResult.ErrorResult("Failed to remove existing installation before reinstall", ex.Message);
                    }
                }

                // ---------------- dependencies ----------------
                Console.WriteLine("ModManager: Resolving dependencies");
                var depResult = await ResolveDependencies(modInfo, gameInstall);
                if (!depResult.Success)
                {
                    Console.WriteLine($"ModManager: Dependency resolution failed: {depResult.Error}");
                    return depResult;
                }
                Console.WriteLine("ModManager: Dependencies resolved successfully");

                // ---------------- install files ----------------
                progress?.Report(0.8);
                Console.WriteLine("ModManager: Progress at 80% - installing mod files");
                var installResult = await InstallModFiles(tempZipPath, modInfo, gameInstall);
                if (!installResult.Success)
                {
                    Console.WriteLine($"ModManager: Install mod files failed: {installResult.Error}");
                    return installResult;
                }

                progress?.Report(0.9);
                Console.WriteLine("ModManager: Progress at 90% - enabling mod");

                // ---------------- auto-enable mod ----------------
                Console.WriteLine($"ModManager: Auto-enabling mod '{modInfo.Name}' after installation");
                var enableResult = await EnableMod(modInfo.Id, gameInstall);
                if (!enableResult.Success)
                {
                    Console.WriteLine($"ModManager: Auto-enable failed: {enableResult.Error}");
                    // don't fail the installation if auto-enable fails, just log it
                    Console.WriteLine($"ModManager: Installation succeeded but mod '{modInfo.Name}' could not be auto-enabled");
                }
                else
                {
                    Console.WriteLine($"ModManager: Mod '{modInfo.Name}' auto-enabled successfully");
                }

                progress?.Report(1);
                return OperationResult.SuccessResult($"Mod '{modInfo.Name}' installed and enabled successfully", modInfo);
            }
            finally
            {
                // ensure the temporary zip is cleaned up to prevent disk bloat
                try
                {
                    if (deleteTemp && !string.IsNullOrEmpty(tempZipPath) && File.Exists(tempZipPath))
                    {
                        File.Delete(tempZipPath);
                        Console.WriteLine($"ModManager: Deleted temporary zip at {tempZipPath}");
                    }
                }
                catch (Exception delEx)
                {
                    // log but do not propagate cleanup errors
                    Console.WriteLine($"ModManager: Failed to delete temp zip '{tempZipPath}': {delEx.Message}");
                }
            }
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
            var folder = modInfo.DirectoryName ?? CleanModNameForFolder(modInfo.Name);
            var modPath = Path.Combine(GetModsDirectoryPath(gameInstall), folder);
            if (Directory.Exists(modPath))
            {
                Directory.Delete(modPath, true);
            }

            // remove plugin mirror
            var pluginMirror = Path.Combine(gameInstall.InstallPath, "BepInEx", "plugins", folder);
            if (Directory.Exists(pluginMirror))
            {
                Directory.Delete(pluginMirror, true);
            }

            // remove from mod list
            await RemoveModFromList(modInfo.Id, gameInstall);

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
            Console.WriteLine($"=== ENABLE MOD DEBUG START ===");
            Console.WriteLine($"EnableMod called for modId: {modId}");
            Console.WriteLine($"Game install path: {gameInstall.InstallPath}");
            
            var modInfo = await GetModInfo(modId, gameInstall);
            if (modInfo == null)
            {
                Console.WriteLine($"ERROR: Mod not found for modId: {modId}");
                return OperationResult.ErrorResult("Mod not found", "Mod does not exist");
            }

            Console.WriteLine($"Mod found - Name: {modInfo.Name}, Current IsEnabled: {modInfo.IsEnabled}");
            Console.WriteLine($"Mod DirectoryName: {modInfo.DirectoryName}");

            if (modInfo.IsEnabled)
            {
                Console.WriteLine($"Mod is already enabled, returning success");
                return OperationResult.SuccessResult("Mod is already enabled");
            }

            // ensure mod id is set before enabling (fixes symlink issues)
            var ensureIdResult = await EnsureModIdIsSet(modId, gameInstall);
            if (!ensureIdResult.Success)
            {
                Console.WriteLine($"ERROR: Failed to ensure mod ID is set: {ensureIdResult.Error}");
                return ensureIdResult;
            }

            // create plugin mirror using symbolic links instead of copying or renaming originals
            var folder = modInfo.DirectoryName ?? modId;
            var modPath = Path.Combine(GetModsDirectoryPath(gameInstall), folder);
            Console.WriteLine($"Mod folder: {folder}");
            Console.WriteLine($"Mod path: {modPath}");
            Console.WriteLine($"Mod path exists: {Directory.Exists(modPath)}");
            
            if (Directory.Exists(modPath))
            {
                Console.WriteLine($"Creating plugin mirror for folder: {folder}");
                MirrorPluginDlls(folder, modPath, gameInstall);
                
                // verify mirror was created
                var pluginMirrorPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "plugins", folder);
                Console.WriteLine($"Plugin mirror path: {pluginMirrorPath}");
                Console.WriteLine($"Plugin mirror exists after creation: {Directory.Exists(pluginMirrorPath)}");
            }

            // update mod status
            Console.WriteLine($"Updating mod status to enabled for modId: {modInfo.Id}");
            await UpdateModStatus(modInfo.Id, true, gameInstall);
            Console.WriteLine($"Mod status update completed");

            // verify the status was updated
            var verifyModInfo = await GetModInfo(modId, gameInstall);
            Console.WriteLine($"Verification - Mod IsEnabled after update: {verifyModInfo?.IsEnabled}");

            Console.WriteLine($"=== ENABLE MOD DEBUG END ===");
            return OperationResult.SuccessResult($"Mod '{modInfo.Name}' enabled successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in EnableMod: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return OperationResult.ErrorResult(ex.Message, "Failed to enable mod");
        }
    }

    public async Task<OperationResult> DisableMod(string modId, GameInstallation gameInstall)
    {
        try
        {
            Console.WriteLine($"=== DISABLE MOD DEBUG START ===");
            Console.WriteLine($"DisableMod called for modId: {modId}");
            Console.WriteLine($"Game install path: {gameInstall.InstallPath}");
            
            var modInfo = await GetModInfo(modId, gameInstall);
            if (modInfo == null)
            {
                Console.WriteLine($"ERROR: Mod not found for modId: {modId}");
                return OperationResult.ErrorResult("Mod not found", "Mod does not exist");
            }

            Console.WriteLine($"Mod found - Name: {modInfo.Name}, Current IsEnabled: {modInfo.IsEnabled}");
            Console.WriteLine($"Mod DirectoryName: {modInfo.DirectoryName}");

            if (!modInfo.IsEnabled)
            {
                Console.WriteLine($"Mod is already disabled, returning success");
                return OperationResult.SuccessResult("Mod is already disabled");
            }

            // ensure mod id is set before disabling (fixes symlink issues)
            var ensureIdResult = await EnsureModIdIsSet(modId, gameInstall);
            if (!ensureIdResult.Success)
            {
                Console.WriteLine($"ERROR: Failed to ensure mod ID is set: {ensureIdResult.Error}");
                return ensureIdResult;
            }

            // clear plugin mirror to disable without touching original files
            var folder = modInfo.DirectoryName ?? modId;
            var pluginsRoot = Path.Combine(gameInstall.InstallPath, "BepInEx", "plugins", folder);
            Console.WriteLine($"Plugin mirror path: {pluginsRoot}");
            Console.WriteLine($"Plugin mirror exists before deletion: {Directory.Exists(pluginsRoot)}");
            
            if (Directory.Exists(pluginsRoot))
            {
                Console.WriteLine($"Deleting plugin mirror directory");
                Directory.Delete(pluginsRoot, true);
                Console.WriteLine($"Plugin mirror deleted");
            }

            // update mod status
            Console.WriteLine($"Updating mod status to disabled for modId: {modInfo.Id}");
            await UpdateModStatus(modInfo.Id, false, gameInstall);
            Console.WriteLine($"Mod status update completed");

            // verify the status was updated
            var verifyModInfo = await GetModInfo(modId, gameInstall);
            Console.WriteLine($"Verification - Mod IsEnabled after update: {verifyModInfo?.IsEnabled}");

            Console.WriteLine($"=== DISABLE MOD DEBUG END ===");
            return OperationResult.SuccessResult($"Mod '{modInfo.Name}' disabled successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in DisableMod: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return OperationResult.ErrorResult(ex.Message, "Failed to disable mod");
        }
    }

    public async Task<List<ModInfo>> GetInstalledMods(GameInstallation gameInstall)
    {
        Console.WriteLine($"ModManager: GetInstalledMods called for game: {gameInstall.Name}");
        var mods = new List<ModInfo>();

        try
        {
            // ensure mods directory exists
            EnsureModsDirectory(gameInstall);
            var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
            Console.WriteLine($"ModManager: Checking for mods list at: {modsListPath}");
            
            // always scan mod directory to ensure we have all mods
            var modDirectory = GetModsDirectoryPath(gameInstall);
            Console.WriteLine($"ModManager: Scanning mod directory: {modDirectory}");
            
            if (Directory.Exists(modDirectory))
            {
                var modDirs = Directory.GetDirectories(modDirectory);
                Console.WriteLine($"ModManager: Found {modDirs.Length} mod directories:");
                
                // create a set to track which mods we've processed
                var processedModIds = new HashSet<string>();
                
                foreach (var modDir in modDirs)
                {
                    var modId = Path.GetFileName(modDir);
                    Console.WriteLine($"ModManager:   - {modId}");

                    var modInfo = await GetModInfo(modId, gameInstall);
                    if (modInfo != null)
                    {
                        Console.WriteLine($"ModManager: Successfully loaded mod info for {modId}");
                        mods.Add(modInfo);
                        processedModIds.Add(modInfo.Id);
                    }
                    else
                    {
                        Console.WriteLine($"ModManager: Failed to load mod info for {modId}");
                    }
                }
                
                // only check mods_list.json if we didn't find any mods in directory
                if (mods.Count == 0 && File.Exists(modsListPath))
                {
                    Console.WriteLine("ModManager: No mods found in directory, checking mods_list.json as fallback");
                    var json = await File.ReadAllTextAsync(modsListPath);
                    try
                    {
                        var listMods = JsonSerializer.Deserialize<List<ModInfo>>(json) ?? new List<ModInfo>();
                        Console.WriteLine($"ModManager: Loaded {listMods.Count} mods from mods_list.json as fallback");
                        mods.AddRange(listMods);
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error deserializing mods_list.json: {ex.Message}");
                        Console.WriteLine($"Problematic JSON: {json}");
                    }
                }
            }
            else
            {
                Console.WriteLine("ModManager: Mod directory does not exist");
                
                // fallback to mods_list.json if directory doesn't exist
                if (File.Exists(modsListPath))
                {
                    Console.WriteLine("ModManager: No mod directory, loading from mods_list.json");
                    var json = await File.ReadAllTextAsync(modsListPath);
                    try
                    {
                        mods = JsonSerializer.Deserialize<List<ModInfo>>(json) ?? new List<ModInfo>();
                        Console.WriteLine($"ModManager: Loaded {mods.Count} mods from mods_list.json");
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error deserializing mods_list.json: {ex.Message}");
                        Console.WriteLine($"Problematic JSON: {json}");
                        mods = new List<ModInfo>();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting installed mods: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine($"ModManager: GetInstalledMods returning {mods.Count} mods");
        return mods;
    }

    public async Task<ModInfo?> GetModInfo(string modId, GameInstallation gameInstall)
    {
        try
        {
            Console.WriteLine($"=== GET MOD INFO DEBUG START ===");
            Console.WriteLine($"GetModInfo called for modId: {modId}");
            Console.WriteLine($"Game install path: {gameInstall.InstallPath}");
            
            var modPath = Path.Combine(GetModsDirectoryPath(gameInstall), modId);
            Console.WriteLine($"Mod path: {modPath}");
            Console.WriteLine($"Mod path exists: {Directory.Exists(modPath)}");
            
            if (!Directory.Exists(modPath))
            {
                Console.WriteLine($"Mod path does not exist, returning null");
                return null;
            }

            // gather plugin dlls inside the mod directory; these remain even when the mod is disabled because we mirror via symlinks.
            var dllFiles = Directory.GetFiles(modPath, "*.dll", SearchOption.AllDirectories);
            Console.WriteLine($"Found {dllFiles.Length} DLL files in mod directory");

            // if no dlls exist at all this is not a valid mod folder
            if (dllFiles.Length == 0)
            {
                Console.WriteLine($"No DLL files found, returning null");
                return null;
            }

            // determine enabled status by checking if the plugin mirror exists within BepInEx/plugins/<modId>
            // note: plugin mirror uses the same folder name as the mod directory (cleaned name)
            var folderName = Path.GetFileName(modPath);
            var pluginMirrorPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "plugins", folderName);
            var isEnabled = Directory.Exists(pluginMirrorPath);
            Console.WriteLine($"Plugin mirror path: {pluginMirrorPath}");
            Console.WriteLine($"Plugin mirror exists: {isEnabled}");
            Console.WriteLine($"Plugin mirror directory check result: {isEnabled}");

            // look for modinfo.json first (primary source)
            var modInfoPath = Path.Combine(modPath, ModInfoFileName);
            Console.WriteLine($"ModInfo file path: {modInfoPath}");
            Console.WriteLine($"ModInfo file exists: {File.Exists(modInfoPath)}");
            
            if (File.Exists(modInfoPath))
            {
                Console.WriteLine($"Loading modinfo.json");
                var json = await File.ReadAllTextAsync(modInfoPath);
                try
                {
                    var modInfo = JsonSerializer.Deserialize<ModInfo>(json);
                    if (modInfo != null)
                    {
                        Console.WriteLine($"ModInfo loaded from JSON - Original IsEnabled: {modInfo.IsEnabled}");
                        Console.WriteLine($"ModInfo loaded from JSON - ID: '{modInfo.Id}', Name: '{modInfo.Name}'");

                        // self-heal missing or mismatched id fields
                        if (string.IsNullOrWhiteSpace(modInfo.Id) || !string.Equals(modInfo.Id, folderName, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"Mod ID is missing or mismatched (current: '{modInfo.Id}', folder: '{folderName}'), overriding with folder name");
                            modInfo.Id = folderName;

                            // write correction back to modinfo.json
                            var correctedJson = JsonSerializer.Serialize(modInfo, new JsonSerializerOptions { WriteIndented = true });
                            await File.WriteAllTextAsync(modInfoPath, correctedJson);
                            Console.WriteLine("Saved corrected modinfo.json with synced ID");
                        }

                        // ensure mod id is set before proceeding (already handled above, keep for clarity)
                        if (string.IsNullOrWhiteSpace(modInfo.Id))
                        {
                            // should not reach here, but fallback to generated id from name
                            var generatedId = modInfo.Name.Replace(" ", "-");
                            modInfo.Id = generatedId;
                        }
                        
                        // get the correct enabled status from mods_list.json using the corrected ID
                        var correctEnabledStatus = await GetModEnabledStatusFromList(modInfo.Id, gameInstall, modInfo.IsEnabled);
                        Console.WriteLine($"Correct enabled status from mods_list.json: {correctEnabledStatus}");
                        modInfo.IsEnabled = correctEnabledStatus;
                        modInfo.IsInstalled = true;
                        
                        Console.WriteLine($"Final ModInfo - Name: {modInfo.Name}, ID: '{modInfo.Id}', IsEnabled: {modInfo.IsEnabled}");
                        
                        // ensure directory name is set so callers can correctly reference the mod folder
                        if (string.IsNullOrWhiteSpace(modInfo.DirectoryName))
                        {
                            modInfo.DirectoryName = Path.GetFileName(modPath);
                            Console.WriteLine($"Set ModInfo.DirectoryName to '{modInfo.DirectoryName}'");
                        }
                        
                        // set icon path if not already set
                        if (string.IsNullOrEmpty(modInfo.IconPath))
                        {
                            var modInfoIconPath = Path.Combine(modPath, "icon.png");
                            if (File.Exists(modInfoIconPath))
                            {
                                modInfo.IconPath = modInfoIconPath;
                                Console.WriteLine($"ModManager: Found icon.png at: {modInfoIconPath}");
                            }
                            else
                            {
                                Console.WriteLine($"ModManager: No icon.png found at: {modInfoIconPath}");
                            }
                        }
                        
                        return modInfo;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error deserializing {ModInfoFileName} for {modId}: {ex.Message}");
                    Console.WriteLine($"Problematic JSON: {json}");
                }
            }

            // fallback to manifest.json if modinfo.json not found or failed to parse
            var manifestPath = Path.Combine(modPath, "manifest.json");
            Console.WriteLine($"Manifest file path: {manifestPath}");
            Console.WriteLine($"Manifest file exists: {File.Exists(manifestPath)}");
            
            if (File.Exists(manifestPath))
            {
                Console.WriteLine($"Loading manifest.json");
                try
                {
                    var manifestJson = await File.ReadAllTextAsync(manifestPath);
                    var manifest = JsonSerializer.Deserialize<JsonElement>(manifestJson);

                    // check mods_list.json for the correct enabled status first
                    var correctEnabledStatus = await GetModEnabledStatusFromList(modId, gameInstall, isEnabled);
                    Console.WriteLine($"Correct enabled status from mods_list.json (manifest fallback): {correctEnabledStatus}");

                    var modName = manifest.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? Path.GetFileName(modPath)).Replace("_", " ") : Path.GetFileName(modPath).Replace("_", " ");
                    var manifestModId = manifest.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Path.GetFileName(modPath) : Path.GetFileName(modPath);
                    
                    // ensure mod id is set - if empty, generate from name
                    if (string.IsNullOrWhiteSpace(manifestModId))
                    {
                        Console.WriteLine($"Manifest mod ID is empty, generating from name");
                        manifestModId = modName.Replace(" ", "-");
                        Console.WriteLine($"Generated ID from manifest: '{manifestModId}'");
                    }
                    
                    var modInfo = new ModInfo
                    {
                        Id = manifestModId,
                        Name = modName,
                        Description = manifest.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? string.Empty : string.Empty,
                        Version = manifest.TryGetProperty("version", out var verEl) ? verEl.GetString() ?? "1.0.0" : 
                                 (manifest.TryGetProperty("version_number", out var verAlt) ? verAlt.GetString() ?? "1.0.0" : "1.0.0"),
                        Author = manifest.TryGetProperty("author", out var authorEl) ? authorEl.GetString() ?? "Unknown" : "Unknown",
                        WebsiteUrl = manifest.TryGetProperty("website_url", out var webEl) ? webEl.GetString() : null,
                        Dependencies = manifest.TryGetProperty("dependencies", out var depsEl) && depsEl.ValueKind == JsonValueKind.Array ?
                            depsEl.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList() : new List<string>(),
                        Tags = manifest.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array ?
                            tagsEl.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList() : new List<string>(),
                        IsInstalled = true,
                        IsEnabled = correctEnabledStatus,
                        DirectoryName = Path.GetFileName(modPath)
                    };

                    // safety: ensure directory name is not empty
                    if (string.IsNullOrWhiteSpace(modInfo.DirectoryName))
                    {
                        modInfo.DirectoryName = Path.GetFileName(modPath);
                        Console.WriteLine($"Set fallback ModInfo.DirectoryName to '{modInfo.DirectoryName}'");
                    }

                    // icon detection
                    var iconPath = manifest.TryGetProperty("icon", out var iconEl) ? iconEl.GetString() : null;
                    if (string.IsNullOrEmpty(iconPath))
                    {
                        var png = Path.Combine(modPath, "icon.png");
                        if (File.Exists(png)) 
                        {
                            iconPath = png;
                            Console.WriteLine($"ModManager: Found icon.png at: {iconPath}");
                        }
                        else
                        {
                            Console.WriteLine($"ModManager: No icon.png found at: {png}");
                        }
                    }
                    else
                    {
                        // make sure the icon path is absolute
                        if (!Path.IsPathRooted(iconPath))
                        {
                            iconPath = Path.Combine(modPath, iconPath);
                        }
                        if (File.Exists(iconPath))
                        {
                            Console.WriteLine($"ModManager: Found icon from manifest at: {iconPath}");
                        }
                        else
                        {
                            Console.WriteLine($"ModManager: Icon from manifest not found at: {iconPath}");
                            iconPath = null;
                        }
                    }

                    modInfo.IconPath = iconPath;

                    return modInfo;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"failed to parse manifest for {modId}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"failed to parse manifest for {modId}: {ex.Message}");
                }
            }

            // fallback: create mod info from directory structure
            var fallbackModInfo = new ModInfo
            {
                Id = modId,
                Name = modId.Replace("_", " ").Replace("-", " "),
                Version = "Unknown",
                Description = "No mod information available",
                Author = "Unknown",
                IsInstalled = true,
                IsEnabled = isEnabled,
                DirectoryName = Path.GetFileName(modPath)
            };
            
            // check for icon.png in fallback case
            var fallbackIconPath = Path.Combine(modPath, "icon.png");
            if (File.Exists(fallbackIconPath))
            {
                fallbackModInfo.IconPath = fallbackIconPath;
                Console.WriteLine($"ModManager: Found icon.png at: {fallbackIconPath}");
            }
            else
            {
                Console.WriteLine($"ModManager: No icon.png found at: {fallbackIconPath}");
            }
            
            return fallbackModInfo;
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

        // check for .app bundles based on executable type, not current platform
        if (ExecutableTypeDetector.IsMacOSAppBundle(executablePath))
        {
            var appBundleName = $"{Path.GetFileNameWithoutExtension(gameInstall.Executable)}.app";
            var appBundlePath = Path.Combine(gameInstall.InstallPath, appBundleName);
            if (Directory.Exists(appBundlePath))
            {
                executablePath = ExecutableTypeDetector.GetAppBundleExecutablePath(appBundlePath);
            }
        }
        
        return File.Exists(executablePath);
    }

    /// <summary>
    /// gets the correct mods directory path accounting for executable type differences
    /// </summary>
    public static string GetModsDirectoryPath(GameInstallation gameInstall)
    {
        return ExecutableTypeDetector.GetModsDirectoryPath(gameInstall);
    }

    public static void EnsureModsDirectory(GameInstallation gameInstall)
    {
        Console.WriteLine($"[DEBUG] EnsureModsDirectory called for game: {gameInstall?.Name}");
        Console.WriteLine($"[DEBUG] Game install path: {gameInstall?.InstallPath}");
        Console.WriteLine($"[DEBUG] Game mod directory: {gameInstall?.ModDirectory}");
        
        var modsRoot = GetModsDirectoryPath(gameInstall);
        Console.WriteLine($"[DEBUG] Mods root path: {modsRoot}");
        Console.WriteLine($"[DEBUG] Mods root exists: {Directory.Exists(modsRoot)}");
        
        if (!Directory.Exists(modsRoot))
        {
            Console.WriteLine($"[DEBUG] Creating mods directory: {modsRoot}");
            try
            {
                Directory.CreateDirectory(modsRoot);
                Console.WriteLine($"[DEBUG] Successfully created mods directory");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Failed to create mods directory: {ex.Message}");
                throw;
            }
        }
        else
        {
            Console.WriteLine($"[DEBUG] Mods directory already exists");
        }
    }

    /// <summary>
    /// cleans mod name for use as folder name by removing whitespace and underscores
    /// </summary>
    private static string CleanModNameForFolder(string modName)
    {
        // 1. basic validation
        if (string.IsNullOrWhiteSpace(modName))
            return "UnknownMod";

        const int MaxLen = 100;

        // allowlist – ascii letters, digits, hyphen, dot
        var sb = new System.Text.StringBuilder(modName.Length);
        foreach (var ch in modName)
        {
            // reject path separators and control chars outright by substituting
            if (ch == '/' || ch == '\\' || char.IsControl(ch))
            {
                sb.Append('-');
                continue;
            }

            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '.')
                sb.Append(ch);
            else
                sb.Append('-'); // disallowed -> dash
        }

        var cleaned = sb.ToString();

        // collapse multiple dashes
        while (cleaned.Contains("--"))
            cleaned = cleaned.Replace("--", "-");

        // trim leading/trailing dots, spaces, and dashes
        cleaned = cleaned.Trim(' ', '.', '-');

        // enforce length limit
        if (cleaned.Length > MaxLen)
            cleaned = cleaned.Substring(0, MaxLen);

        if (string.IsNullOrEmpty(cleaned))
            cleaned = "UnknownMod";

        // avoid windows reserved device names (case-insensitive)
        string[] reserved =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        if (reserved.Any(r => string.Equals(r, cleaned, StringComparison.OrdinalIgnoreCase)))
            cleaned += "_";

        return cleaned;
    }

    /// <summary>
    /// ensures that the mod has a valid ID by checking modinfo.json and setting it from name if empty
    /// </summary>
    private async Task<OperationResult> EnsureModIdIsSet(string modId, GameInstallation gameInstall)
    {
        try
        {
            Console.WriteLine($"=== ENSURE MOD ID DEBUG START ===");
            Console.WriteLine($"EnsureModIdIsSet called for modId: {modId}");
            
            var modPath = Path.Combine(GetModsDirectoryPath(gameInstall), modId);
            Console.WriteLine($"Mod path: {modPath}");
            
            if (!Directory.Exists(modPath))
            {
                Console.WriteLine($"Mod path does not exist");
                return OperationResult.ErrorResult("Mod directory not found", "Mod directory does not exist");
            }

            var modInfoPath = Path.Combine(modPath, ModInfoFileName);
            Console.WriteLine($"ModInfo file path: {modInfoPath}");
            Console.WriteLine($"ModInfo file exists: {File.Exists(modInfoPath)}");
            
            if (!File.Exists(modInfoPath))
            {
                Console.WriteLine($"ModInfo file does not exist, skipping ID check");
                return OperationResult.SuccessResult("No modinfo.json found, skipping ID check");
            }

            // read and parse modinfo.json
            var json = await File.ReadAllTextAsync(modInfoPath);
            Console.WriteLine($"ModInfo JSON content: {json}");
            
            try
            {
                var modInfo = JsonSerializer.Deserialize<ModInfo>(json);
                if (modInfo == null)
                {
                    Console.WriteLine($"Failed to deserialize modinfo.json");
                    return OperationResult.ErrorResult("Failed to parse modinfo.json", "Invalid JSON format");
                }

                Console.WriteLine($"Current mod ID: '{modInfo.Id}'");
                Console.WriteLine($"Current mod name: '{modInfo.Name}'");

                var folderName = Path.GetFileName(modPath);

                // if ID is empty or mismatched, correct it to folder name
                if (string.IsNullOrWhiteSpace(modInfo.Id) || !string.Equals(modInfo.Id, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Mod ID is empty or mismatched (current: '{modInfo.Id}', folder: '{folderName}') - overriding");
                    modInfo.Id = folderName;

                    // write the updated modinfo.json back to file
                    var updatedJson = JsonSerializer.Serialize(modInfo, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(modInfoPath, updatedJson);

                    Console.WriteLine("Successfully saved corrected modinfo.json");
                    return OperationResult.SuccessResult("Synced mod ID to folder name");
                }
                else
                {
                    Console.WriteLine("Mod ID already matches folder name");
                    return OperationResult.SuccessResult("Mod ID is consistent");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error deserializing modinfo.json: {ex.Message}");
                return OperationResult.ErrorResult($"Failed to parse modinfo.json: {ex.Message}", "Invalid JSON format");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in EnsureModIdIsSet: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return OperationResult.ErrorResult(ex.Message, "Failed to ensure mod ID is set");
        }
        finally
        {
            Console.WriteLine($"=== ENSURE MOD ID DEBUG END ===");
        }
    }

    /// <summary>
    /// extracts the mod name from manifest json to use as fallback when id is not available
    /// </summary>
    private static string ExtractModNameFromManifest(string manifestJson)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<JsonElement>(manifestJson);
            
            // try to get the name field first
            if (manifest.TryGetProperty("name", out var nameEl))
            {
                var name = nameEl.GetString();
                if (!string.IsNullOrEmpty(name))
                {
                    return CleanModNameForFolder(name);
                }
            }
            
            // if no name, try to get the id field
            if (manifest.TryGetProperty("id", out var idEl))
            {
                var id = idEl.GetString();
                if (!string.IsNullOrEmpty(id))
                {
                    return CleanModNameForFolder(id);
                }
            }
            
            // fallback to a generic name
            return "UnknownMod";
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"failed to extract mod name from manifest: {ex.Message}");
            return "UnknownMod";
        }
    }

    /// <summary>
    /// parses manifest.json content to ModInfo, handling field name differences
    /// </summary>
    private static ModInfo? ParseManifestToModInfo(string manifestJson, string fallbackId, string? owner = null)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<JsonElement>(manifestJson);

            var modInfo = new ModInfo
            {
                Id = manifest.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? fallbackId : fallbackId,
                Name = manifest.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? fallbackId).Replace("_", " ").Replace("-", " ") : fallbackId.Replace("_", " ").Replace("-", " "),
                Description = manifest.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? string.Empty : string.Empty,
                Version = manifest.TryGetProperty("version", out var verEl) ? verEl.GetString() ?? "1.0.0" : 
                         (manifest.TryGetProperty("version_number", out var verAlt) ? verAlt.GetString() ?? "1.0.0" : "1.0.0"),
                // use owner from API response as primary source, fallback to manifest author, then "Unknown"
                Author = !string.IsNullOrEmpty(owner) ? owner : 
                         (manifest.TryGetProperty("author", out var authorEl) ? authorEl.GetString() ?? "Unknown" : "Unknown"),
                WebsiteUrl = manifest.TryGetProperty("website_url", out var webEl) ? webEl.GetString() : null,
                Dependencies = manifest.TryGetProperty("dependencies", out var depsEl) && depsEl.ValueKind == JsonValueKind.Array ?
                    depsEl.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList() : new List<string>(),
                Tags = manifest.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array ?
                    tagsEl.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList() : new List<string>(),
                IsInstalled = true,
                IsEnabled = false, // will be set properly during installation
                DirectoryName = CleanModNameForFolder(manifest.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? fallbackId : fallbackId)
            };

            return modInfo;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"failed to parse manifest: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"failed to parse manifest: {ex.Message}");
            return null;
        }
    }

    private static void MirrorPluginDlls(string modId, string modsPath, GameInstallation gameInstall)
    {
        var pluginsRoot = Path.Combine(gameInstall.InstallPath, "BepInEx", "plugins", modId);

        // clear existing mirror (symlinks or files)
        if (Directory.Exists(pluginsRoot))
        {
            Directory.Delete(pluginsRoot, true);
        }

        // symlink the entire mod folder instead of individual files
        CreateSymbolicLinkForDirectory(pluginsRoot, modsPath);
    }

    /// <summary>
    /// creates a symbolic link for a file, with fallback to copying if symlinks aren't supported
    /// </summary>
    private static void CreateSymbolicLinkForFile(string linkPath, string targetPath)
    {
        try
        {
            // attempt to create symbolic link (preferred – no duplication)
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (UnauthorizedAccessException)
        {
            // fallback to hard copy if symlink creation is not permitted (developer mode disabled)
            File.Copy(targetPath, linkPath, true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Copy(targetPath, linkPath, true);
        }
    }

    /// <summary>
    /// creates a symbolic link for a directory, using junctions on Windows and symlinks on other platforms
    /// </summary>
    private static void CreateSymbolicLinkForDirectory(string linkPath, string targetPath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // on windows, use directory junctions for folders
                CreateDirectoryJunction(linkPath, targetPath);
            }
            else
            {
                // on unix-like systems, use symbolic links for directories
                Directory.CreateSymbolicLink(linkPath, targetPath);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // fallback to recursive copy if junction/symlink creation is not permitted
            CopyDirectoryRecursive(targetPath, linkPath);
        }
        catch (PlatformNotSupportedException)
        {
            CopyDirectoryRecursive(targetPath, linkPath);
        }
    }

    /// <summary>
    /// creates a directory junction on Windows using mklink command
    /// </summary>
    private static void CreateDirectoryJunction(string junctionPath, string targetPath)
    {
        try
        {
            // use mklink /J to create a directory junction
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Failed to create junction: {error}");
            }
        }
        catch (Exception ex)
        {
            // if junction creation fails, fall back to recursive copy
            CopyDirectoryRecursive(targetPath, junctionPath);
        }
    }

    /// <summary>
    /// recursively copies a directory as fallback when symlinks/junctions aren't available
    /// </summary>
    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSubDir);
        }
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

    /// <summary>
    /// safely extracts a zip archive to the specified destination, validating paths to mitigate zip-slip.
    /// </summary>
    /// <param name="zipPath">path to the zip archive</param>
    /// <param name="destinationDirectory">directory to extract to</param>
    private static void ExtractZipSafely(string zipPath, string destinationDirectory)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var fullDestRoot = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.FullName))
                continue;

            var fullDestPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));

            var comparison = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!fullDestPath.StartsWith(fullDestRoot, comparison))
                continue; // zip-slip attempt

            // directory entry – Name is empty when it represents a directory
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(fullDestPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath)!);
            entry.ExtractToFile(fullDestPath, true);
        }
    }

    /// <summary>
    /// returns a sanitized folder name for the mod, preferring its canonical id when available.
    /// </summary>
    private static string GetFolderName(ModInfo modInfo)
    {
        var basis = !string.IsNullOrEmpty(modInfo.Id) ? modInfo.Id : modInfo.Name;
        return CleanModNameForFolder(basis);
    }

    private async Task<OperationResult> ExtractAndAnalyzeMod(string zipPath, GameInstallation gameInstall, string? owner = null)
    {
        Console.WriteLine($"ModManager: ExtractAndAnalyzeMod called with zipPath: {zipPath}");
        Console.WriteLine($"ModManager: Owner from API: {owner ?? "null"}");

        string tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Console.WriteLine($"ModManager: Creating temp extract path: {tempExtractPath}");
            Directory.CreateDirectory(tempExtractPath);

            Console.WriteLine("ModManager: Extracting zip file to temp directory (safe extraction)");
            ExtractZipSafely(zipPath, tempExtractPath);

            // list all extracted files for debugging
            var allFiles = Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories);
            Console.WriteLine($"ModManager: Extracted {allFiles.Length} files:");
            foreach (var file in allFiles.Take(10)) // limit to first 10 files
            {
                Console.WriteLine($"ModManager:   - {Path.GetRelativePath(tempExtractPath, file)}");
            }
            if (allFiles.Length > 10)
            {
                Console.WriteLine($"ModManager:   ... and {allFiles.Length - 10} more files");
            }

            // look for mod manifest
            var manifestPath = Path.Combine(tempExtractPath, ModManifestFileName);
            Console.WriteLine($"ModManager: Looking for manifest at: {manifestPath}");
            ModInfo? modInfo = null;

            if (File.Exists(manifestPath))
            {
                Console.WriteLine("ModManager: Found manifest.json in root directory");
                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                Console.WriteLine($"ModManager: Manifest content length: {manifestJson.Length} characters");
                Console.WriteLine($"ModManager: Manifest content preview: {manifestJson.Substring(0, Math.Min(200, manifestJson.Length))}...");
                
                // extract mod name from manifest to use as fallback instead of "temp"
                var fallbackName = ExtractModNameFromManifest(manifestJson);
                modInfo = ParseManifestToModInfo(manifestJson, fallbackName, owner);
                Console.WriteLine($"ModManager: Parsed mod info - Name: {modInfo?.Name}, ID: {modInfo?.Id}");
            }
            else
            {
                Console.WriteLine("ModManager: No manifest.json in root, searching recursively");
                // search recursively for manifest.json (handles archives with nested folders)
                var nestedManifest = Directory.GetFiles(tempExtractPath, ModManifestFileName, SearchOption.AllDirectories).FirstOrDefault();
                if (nestedManifest != null)
                {
                    Console.WriteLine($"ModManager: Found nested manifest at: {nestedManifest}");
                    var manifestJson = await File.ReadAllTextAsync(nestedManifest);
                    Console.WriteLine($"ModManager: Nested manifest content length: {manifestJson.Length} characters");
                    Console.WriteLine($"ModManager: Nested manifest content preview: {manifestJson.Substring(0, Math.Min(200, manifestJson.Length))}...");
                    
                    // extract mod name from manifest to use as fallback instead of "temp"
                    var fallbackName = ExtractModNameFromManifest(manifestJson);
                    modInfo = ParseManifestToModInfo(manifestJson, fallbackName, owner);
                    Console.WriteLine($"ModManager: Parsed nested mod info - Name: {modInfo?.Name}, ID: {modInfo?.Id}");
                }
                else
                {
                    Console.WriteLine("ModManager: No manifest.json found anywhere in archive");
                }
            }

            // if no manifest, try to infer from directory structure
            if (modInfo == null)
            {
                Console.WriteLine("ModManager: Attempting to infer mod info from directory structure");
                modInfo = await InferModInfo(tempExtractPath, owner);
                Console.WriteLine($"ModManager: Inferred mod info - Name: {modInfo?.Name}, ID: {modInfo?.Id}");
            }

            if (modInfo == null)
            {
                Console.WriteLine("ModManager: Could not determine mod information, cleaning up and returning error");
                return OperationResult.ErrorResult("Could not determine mod information", "Invalid mod format");
            }

            Console.WriteLine($"ModManager: Final mod info - Name: '{modInfo.Name}', ID: '{modInfo.Id}', Author: '{modInfo.Author}', Version: '{modInfo.Version}'");

            return OperationResult.SuccessResult("Mod analyzed successfully", modInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ModManager: ExtractAndAnalyzeMod exception: {ex.Message}");
            Console.WriteLine($"ModManager: ExtractAndAnalyzeMod stack trace: {ex.StackTrace}");
            return OperationResult.ErrorResult(ex.Message, "Failed to analyze mod");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempExtractPath))
                {
                    Directory.Delete(tempExtractPath, true);
                }
            }
            catch { /* ignore cleanup errors */ }
        }
    }

    private async Task<ModInfo?> InferModInfo(string extractPath, string? owner = null)
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
                Author = !string.IsNullOrEmpty(owner) ? owner : "Unknown",
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
                // skip bepinex packs – these are handled by the manager separately
                if (IsBepInExDependency(dependency))
                    continue;

                if (!installedMods.Any(m => m.Id.Equals(dependency, StringComparison.OrdinalIgnoreCase)))
                {
                    missingDependencies.Add(dependency);
                }
            }

            // attempt to download and install each missing dependency
            foreach (var dep in missingDependencies.ToList())
            {
                if (!TryParseThunderstoreDependency(dep, out var author, out var package, out var version))
                    continue; // malformed dependency string – ignore

                var url = $"https://thunderstore.io/package/download/{author}/{package}/{version}/";
                var installResult = await InstallModFromZip(url, gameInstall);
                if (installResult.Success)
                {
                    missingDependencies.Remove(dep);
                    installedMods.Add((ModInfo)installResult.Data!);
                }
                else
                {
                    return OperationResult.ErrorResult($"Failed to install dependency '{dep}': {installResult.Error}", "Dependency installation failed");
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

    /// <summary>
    /// returns true when the dependency string refers to a BepInEx pack, which the mod manager handles separately.
    /// </summary>
    private static bool IsBepInExDependency(string dependency)
    {
        return dependency.StartsWith("BepInEx-", StringComparison.OrdinalIgnoreCase) ||
               dependency.Contains("BepInExPack", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// parses a thunderstore dependency string (Author-Package-Version) into its constituent parts.
    /// returns false when the string cannot be parsed.
    /// </summary>
    private static bool TryParseThunderstoreDependency(string dependency, out string author, out string package, out string version)
    {
        author = package = version = string.Empty;
        var parts = dependency.Split('-');
        if (parts.Length < 3) return false;

        author = parts[0];
        version = parts[^1];
        package = string.Join('-', parts.Skip(1).Take(parts.Length - 2));
        return true;
    }

    private async Task<OperationResult> InstallModFiles(string zipPath, ModInfo modInfo, GameInstallation gameInstall)
    {
        Console.WriteLine($"ModManager: InstallModFiles called for mod: {modInfo.Name}");
        
        try
        {
            // derive a sanitized folder name from mod id (preferred) or name
            var folderName = GetFolderName(modInfo);
            Console.WriteLine($"ModManager: Original mod name: '{modInfo.Name}' -> Folder name: '{folderName}'");
            
            // persist directory name separately so we don't mutate the canonical id
            modInfo.DirectoryName = folderName;
            
            var modsBasePath = GetModsDirectoryPath(gameInstall);
            var dir = modInfo.DirectoryName ?? CleanModNameForFolder(modInfo.Name);
            var modPath = Path.Combine(modsBasePath, dir);
            Console.WriteLine($"ModManager: Full mod installation path: {modPath}");
            
            // create mod directory
            Console.WriteLine("ModManager: Creating mod directory");
            Directory.CreateDirectory(modPath);

            // extract mod files using safe extraction
            Console.WriteLine($"ModManager: Extracting zip file from {zipPath} to {modPath} (safe extraction)");
            ExtractZipSafely(zipPath, modPath);

            // list extracted files for debugging
            var extractedFiles = Directory.GetFiles(modPath, "*", SearchOption.AllDirectories);
            Console.WriteLine($"ModManager: Extracted {extractedFiles.Length} files to mod directory:");
            foreach (var file in extractedFiles.Take(10)) // limit to first 10 files
            {
                Console.WriteLine($"ModManager:   - {Path.GetRelativePath(modPath, file)}");
            }
            if (extractedFiles.Length > 10)
            {
                Console.WriteLine($"ModManager:   ... and {extractedFiles.Length - 10} more files");
            }

            // save mod info
            var modInfoPath = Path.Combine(modPath, ModInfoFileName);
            Console.WriteLine($"ModManager: Saving mod info to: {modInfoPath}");
            var modInfoJson = JsonSerializer.Serialize(modInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(modInfoPath, modInfoJson);
            Console.WriteLine("ModManager: Mod info saved successfully");

            // ensure mod ID is set to directory name (fixes ID mismatch issues)
            Console.WriteLine($"ModManager: Ensuring mod ID is set to directory name: {folderName}");
            var ensureIdResult = await EnsureModIdIsSet(folderName, gameInstall);
            if (!ensureIdResult.Success)
            {
                Console.WriteLine($"ModManager: Warning - Failed to ensure mod ID is set: {ensureIdResult.Error}");
                // don't fail installation for this, just log the warning
            }
            else
            {
                Console.WriteLine("ModManager: Mod ID successfully ensured");
            }

            // add to mods list
            Console.WriteLine("ModManager: Adding mod to installed mods list");
            await AddModToList(modInfo, gameInstall);
            Console.WriteLine("ModManager: Mod added to list successfully");

            // mirror dlls into BepInEx/plugins so chainloader picks them up
            Console.WriteLine($"ModManager: Mirroring DLLs using folder name: {folderName}");
            MirrorPluginDlls(folderName, modPath, gameInstall);
            Console.WriteLine("ModManager: DLL mirroring completed");

            Console.WriteLine("ModManager: Mod files installation completed successfully");
            return OperationResult.SuccessResult("Mod files installed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ModManager: InstallModFiles exception: {ex.Message}");
            Console.WriteLine($"ModManager: InstallModFiles stack trace: {ex.StackTrace}");
            return OperationResult.ErrorResult(ex.Message, "Failed to install mod files");
        }
    }

    private async Task AddModToList(ModInfo modInfo, GameInstallation gameInstall)
    {
        Console.WriteLine($"ModManager: AddModToList called for mod: {modInfo.Name} (ID: {modInfo.Id})");
        
        try
        {
            var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
            Console.WriteLine($"ModManager: Mods list path: {modsListPath}");
            var mods = new List<ModInfo>();

            if (File.Exists(modsListPath))
            {
                Console.WriteLine("ModManager: Loading existing mods list");
                var json = await File.ReadAllTextAsync(modsListPath);
                try
                {
                    mods = JsonSerializer.Deserialize<List<ModInfo>>(json) ?? new List<ModInfo>();
                    Console.WriteLine($"ModManager: Loaded {mods.Count} existing mods from list");
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error deserializing mods_list.json: {ex.Message}");
                    Console.WriteLine($"Problematic JSON: {json}");
                    mods = new List<ModInfo>();
                }
            }
            else
            {
                Console.WriteLine("ModManager: No existing mods list found, starting fresh");
            }

            // remove existing entry if present
            var removedCount = mods.RemoveAll(m => m.Id == modInfo.Id);
            if (removedCount > 0)
            {
                Console.WriteLine($"ModManager: Removed {removedCount} existing entries for mod {modInfo.Id}");
            }
            
            // add new entry
            Console.WriteLine($"ModManager: Adding mod {modInfo.Name} to mods list");
            mods.Add(modInfo);

            // save updated list
            Console.WriteLine($"ModManager: Saving {mods.Count} mods to mods_list.json");
            var updatedJson = JsonSerializer.Serialize(mods, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(modsListPath, updatedJson);
            Console.WriteLine("ModManager: Successfully wrote mods list to file");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding mod to list: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
            Console.WriteLine($"=== UPDATE MOD STATUS DEBUG START ===");
            Console.WriteLine($"UpdateModStatus called for modId: {modId}, isEnabled: {isEnabled}");
            Console.WriteLine($"Game install path: {gameInstall.InstallPath}");
            
            var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
            Console.WriteLine($"Mods list path: {modsListPath}");
            Console.WriteLine($"Mods list file exists: {File.Exists(modsListPath)}");
            
            if (!File.Exists(modsListPath))
            {
                Console.WriteLine($"Mods list file does not exist, creating it");
                var emptyMods = new List<ModInfo>();
                var emptyJson = JsonSerializer.Serialize(emptyMods, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(modsListPath, emptyJson);
                Console.WriteLine($"Created empty mods list file");
            }

            var json = await File.ReadAllTextAsync(modsListPath);
            Console.WriteLine($"Current mods list JSON length: {json.Length}");
            Console.WriteLine($"Current mods list JSON: {json}");
            
            var mods = JsonSerializer.Deserialize<List<ModInfo>>(json) ?? new List<ModInfo>();
            Console.WriteLine($"Deserialized {mods.Count} mods from list");

            // update mod status
            var mod = mods.FirstOrDefault(m => m.Id == modId || (!string.IsNullOrWhiteSpace(m.DirectoryName) && m.DirectoryName == modId));
            if (mod != null)
            {
                Console.WriteLine($"Found mod in list - Name: {mod.Name}, Current IsEnabled: {mod.IsEnabled}");
                mod.IsEnabled = isEnabled;
                Console.WriteLine($"Updated mod IsEnabled to: {mod.IsEnabled}");
            }
            else
            {
                Console.WriteLine($"Mod not found in mods list, adding new entry");
                // if mod not found, we need to get its info and add it
                var modInfo = await GetModInfo(modId, gameInstall);
                if (modInfo != null)
                {
                    modInfo.IsEnabled = isEnabled;
                    mods.Add(modInfo);
                    Console.WriteLine($"Added mod to list - Name: {modInfo.Name}, IsEnabled: {modInfo.IsEnabled}");
                }
                else
                {
                    Console.WriteLine($"ERROR: Could not get mod info for {modId}");
                }
            }

            // ensure duplicates (id vs directoryname) are removed keeping latest
            mods = mods
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .ToList();

            // save updated list
            var updatedJson = JsonSerializer.Serialize(mods, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Updated JSON length: {updatedJson.Length}");
            Console.WriteLine($"Updated JSON: {updatedJson}");
            
            await File.WriteAllTextAsync(modsListPath, updatedJson);
            Console.WriteLine($"Saved updated mods list to file");
            
            Console.WriteLine($"=== UPDATE MOD STATUS DEBUG END ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in UpdateModStatus: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
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

            var modPath = Path.Combine(GetModsDirectoryPath(gameInstall), modInfo.DirectoryName ?? CleanModNameForFolder(modInfo.Name));
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

    private async Task<OperationResult> DownloadModWithProgress(string url, IProgress<double>? progress, CancellationToken token)
    {
        try
        {
            var tempPath = Path.GetTempFileName();
            var tempZipPath = Path.ChangeExtension(tempPath, ".zip");
            File.Delete(tempPath);

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            using var resp = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1L;
            await using var stream = await resp.Content.ReadAsStreamAsync(token);
            await using var fs = File.OpenWrite(tempZipPath);
            var buffer = new byte[81920];
            long read = 0;
            int bytes;
            while ((bytes = await stream.ReadAsync(buffer, token)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, bytes), token);
                read += bytes;
                if (total > 0)
                {
                    progress?.Report(Math.Min(0.3, 0.3 * (read / (double)total)));
                }
            }
            return OperationResult.SuccessResult("downloaded", tempZipPath);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "download failed");
        }
    }

    /// <summary>
    /// gets the enabled status for a mod from mods_list.json, falling back to plugin mirror check
    /// </summary>
    private async Task<bool> GetModEnabledStatusFromList(string modId, GameInstallation gameInstall, bool fallbackStatus)
    {
        try
        {
            Console.WriteLine($"=== GET MOD ENABLED STATUS FROM LIST DEBUG START ===");
            Console.WriteLine($"GetModEnabledStatusFromList called for modId: {modId}, fallbackStatus: {fallbackStatus}");
            
            var modsListPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "mods_list.json");
            Console.WriteLine($"Mods list path: {modsListPath}");
            Console.WriteLine($"Mods list file exists: {File.Exists(modsListPath)}");
            
            if (!File.Exists(modsListPath))
            {
                Console.WriteLine($"Mods list file does not exist, returning fallback status: {fallbackStatus}");
                return fallbackStatus;
            }

            var json = await File.ReadAllTextAsync(modsListPath);
            Console.WriteLine($"Mods list JSON length: {json.Length}");
            Console.WriteLine($"Mods list JSON: {json}");
            
            var mods = JsonSerializer.Deserialize<List<ModInfo>>(json) ?? new List<ModInfo>();
            Console.WriteLine($"Deserialized {mods.Count} mods from list");

            var mod = mods.FirstOrDefault(m => m.Id == modId || (!string.IsNullOrWhiteSpace(m.DirectoryName) && m.DirectoryName == modId));
            if (mod != null)
            {
                Console.WriteLine($"Found mod in list - Name: {mod.Name}, IsEnabled: {mod.IsEnabled}");
                Console.WriteLine($"Returning status from list: {mod.IsEnabled}");
                return mod.IsEnabled;
            }
            else
            {
                Console.WriteLine($"Mod not found in list, returning fallback status: {fallbackStatus}");
                return fallbackStatus;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in GetModEnabledStatusFromList: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine($"Returning fallback status: {fallbackStatus}");
            return fallbackStatus;
        }
        finally
        {
            Console.WriteLine($"=== GET MOD ENABLED STATUS FROM LIST DEBUG END ===");
        }
    }
}


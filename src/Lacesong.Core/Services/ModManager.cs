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
        => await InstallModFromZip(source, gameInstall, progress: null, CancellationToken.None);

    public async Task<OperationResult> InstallModFromZip(string source, GameInstallation gameInstall, IProgress<double>? progress, CancellationToken token = default)
    {
        Console.WriteLine($"ModManager: InstallModFromZip called with source: {source}");
        Console.WriteLine($"ModManager: GameInstall - Name: {gameInstall.Name}, Path: {gameInstall.InstallPath}");
        
        try
        {
            progress?.Report(0.05);
            Console.WriteLine("ModManager: Ensuring mods directory exists");
            // ensure mods directory exists
            EnsureModsDirectory(gameInstall);
            var modsDir = GetModsDirectoryPath(gameInstall);
            Console.WriteLine($"ModManager: Mods directory: {modsDir}");
            
            if (!ValidateGameInstall(gameInstall))
            {
                Console.WriteLine("ModManager: Game installation validation failed");
                return OperationResult.ErrorResult("Invalid game installation", "Game installation validation failed");
            }
            Console.WriteLine("ModManager: Game installation validation passed");

            if (!_bepInExManager.IsBepInExInstalled(gameInstall))
            {
                Console.WriteLine("ModManager: BepInEx is not installed");
                return OperationResult.ErrorResult("BepInEx is not installed", "BepInEx required for mod installation");
            }
            Console.WriteLine("ModManager: BepInEx validation passed");

            string tempZipPath;

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

            Console.WriteLine("ModManager: Extracting and analyzing mod");
            var extractResult = await ExtractAndAnalyzeMod(tempZipPath, gameInstall);
            if (!extractResult.Success) 
            {
                Console.WriteLine($"ModManager: Extract and analyze failed: {extractResult.Error}");
                return extractResult;
            }
            progress?.Report(0.6);
            Console.WriteLine("ModManager: Progress at 60% - extraction complete");

            var modInfo = extractResult.Data as ModInfo ?? throw new("failed to parse mod info");
            Console.WriteLine($"ModManager: Mod info parsed - Name: {modInfo.Name}, ID: {modInfo.Id}, Author: {modInfo.Author}");

            // allow reinstall even if already present in mods_list.json; if present on disk, clear existing before reinstall
            Console.WriteLine("ModManager: Checking for existing installation on disk to allow reinstall");
            var modsBasePath = GetModsDirectoryPath(gameInstall);
            var existingModPath = Path.Combine(modsBasePath, CleanModNameForFolder(modInfo.Name));
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

            Console.WriteLine("ModManager: Resolving dependencies");
            var depResult = await ResolveDependencies(modInfo, gameInstall);
            if (!depResult.Success) 
            {
                Console.WriteLine($"ModManager: Dependency resolution failed: {depResult.Error}");
                return depResult;
            }
            Console.WriteLine("ModManager: Dependencies resolved successfully");

            progress?.Report(0.8);
            Console.WriteLine("ModManager: Progress at 80% - installing mod files");
            var installResult = await InstallModFiles(tempZipPath, modInfo, gameInstall);
            if (!installResult.Success) 
            {
                Console.WriteLine($"ModManager: Install mod files failed: {installResult.Error}");
                return installResult;
            }

            progress?.Report(1);
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
            var modPath = Path.Combine(GetModsDirectoryPath(gameInstall), modId);
            if (Directory.Exists(modPath))
            {
                Directory.Delete(modPath, true);
            }

            // remove plugin mirror
            var pluginMirror = Path.Combine(gameInstall.InstallPath, "BepInEx", "plugins", modId);
            if (Directory.Exists(pluginMirror))
            {
                Directory.Delete(pluginMirror, true);
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

            // create plugin mirror using symbolic links instead of copying or renaming originals
            var modPath = Path.Combine(GetModsDirectoryPath(gameInstall), modId);
            if (Directory.Exists(modPath))
            {
                MirrorPluginDlls(modId, modPath, gameInstall);
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

            // clear plugin mirror to disable without touching original files
            var pluginsRoot = Path.Combine(gameInstall.InstallPath, "BepInEx", "plugins", modId);
            if (Directory.Exists(pluginsRoot))
            {
                Directory.Delete(pluginsRoot, true);
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
            var modPath = Path.Combine(GetModsDirectoryPath(gameInstall), modId);
            if (!Directory.Exists(modPath))
                return null;

            // gather plugin dlls inside the mod directory; these remain even when the mod is disabled because we mirror via symlinks.
            var dllFiles = Directory.GetFiles(modPath, "*.dll", SearchOption.AllDirectories);

            // if no dlls exist at all this is not a valid mod folder
            if (dllFiles.Length == 0)
                return null;

            // determine enabled status by checking if the plugin mirror exists within BepInEx/plugins/<modId>
            // note: plugin mirror uses the same folder name as the mod directory (cleaned name)
            var pluginMirrorPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "plugins", modId);
            var isEnabled = Directory.Exists(pluginMirrorPath);

            // look for manifest.json first (new format)
            var manifestPath = Path.Combine(modPath, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    var manifestJson = await File.ReadAllTextAsync(manifestPath);
                    var manifest = JsonSerializer.Deserialize<JsonElement>(manifestJson);

                    var modInfo = new ModInfo
                    {
                        Id = manifest.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Path.GetFileName(modPath) : Path.GetFileName(modPath),
                        Name = manifest.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? Path.GetFileName(modPath)).Replace("_", " ") : Path.GetFileName(modPath).Replace("_", " "),
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
                        IsEnabled = isEnabled
                    };

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

            // fallback to modinfo.json if manifest.json not found or failed to parse
            var modInfoPath = Path.Combine(modPath, ModInfoFileName);
            if (File.Exists(modInfoPath))
            {
                var json = await File.ReadAllTextAsync(modInfoPath);
                try
                {
                    var modInfo = JsonSerializer.Deserialize<ModInfo>(json);
                    if (modInfo != null)
                    {
                        modInfo.IsEnabled = isEnabled;
                        modInfo.IsInstalled = true;
                        return modInfo;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error deserializing {ModInfoFileName} for {modId}: {ex.Message}");
                    Console.WriteLine($"Problematic JSON: {json}");
                }
            }

            // fallback: create mod info from directory structure
            return new ModInfo
            {
                Id = modId,
                Name = modId.Replace("_", " "),
                Version = "Unknown",
                Description = "No mod information available",
                Author = "Unknown",
                IsInstalled = true,
                IsEnabled = isEnabled
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

        if (PlatformDetector.IsMacOS)
        {
            var appBundleName = $"{Path.GetFileNameWithoutExtension(gameInstall.Executable)}.app";
            var appBundlePath = Path.Combine(gameInstall.InstallPath, appBundleName);
            if (Directory.Exists(appBundlePath))
            {
                executablePath = Path.Combine(appBundlePath, "Contents", "MacOS", gameInstall.Executable);
            }
        }
        
        return File.Exists(executablePath);
    }

    /// <summary>
    /// gets the correct mods directory path accounting for platform differences
    /// </summary>
    public static string GetModsDirectoryPath(GameInstallation gameInstall)
    {
        // on macos, unity games use .app bundles with data stored at Contents/Resources/Data/
        if (PlatformDetector.IsMacOS)
        {
            var appBundlePath = Path.Combine(gameInstall.InstallPath, $"{Path.GetFileNameWithoutExtension(gameInstall.Executable)}.app");
            if (Directory.Exists(appBundlePath))
            {
                // macos unity games store data in Contents/Resources/Data/
                return Path.Combine(appBundlePath, "Contents", "Resources", "Data", "Managed", "Mods");
            }
        }
        
        // fallback to standard path for windows, linux, or if .app bundle not found
        return Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory);
    }

    public static void EnsureModsDirectory(GameInstallation gameInstall)
    {
        var modsRoot = GetModsDirectoryPath(gameInstall);
        if (!Directory.Exists(modsRoot))
        {
            Directory.CreateDirectory(modsRoot);
        }
    }

    /// <summary>
    /// cleans mod name for use as folder name by removing whitespace and underscores
    /// </summary>
    private static string CleanModNameForFolder(string modName)
    {
        if (string.IsNullOrEmpty(modName))
            return "UnknownMod";
            
        // remove whitespace and underscores, keep other characters
        return modName.Replace(" ", "").Replace("_", "");
    }

    /// <summary>
    /// parses manifest.json content to ModInfo, handling field name differences
    /// </summary>
    private static ModInfo? ParseManifestToModInfo(string manifestJson, string fallbackId)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<JsonElement>(manifestJson);

            var modInfo = new ModInfo
            {
                Id = manifest.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? fallbackId : fallbackId,
                Name = manifest.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? fallbackId).Replace("_", " ") : fallbackId.Replace("_", " "),
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
                IsEnabled = false // will be set properly during installation
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

        Directory.CreateDirectory(pluginsRoot);

        var dllFiles = Directory.GetFiles(modsPath, "*.dll", SearchOption.AllDirectories);

        foreach (var dll in dllFiles)
        {
            var linkPath = Path.Combine(pluginsRoot, Path.GetFileName(dll));
            try
            {
                // attempt to create symbolic link (preferred – no duplication)
                File.CreateSymbolicLink(linkPath, dll);
            }
            catch (UnauthorizedAccessException)
            {
                // fallback to hard copy if symlink creation is not permitted (developer mode disabled)
                File.Copy(dll, linkPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(dll, linkPath, true);
            }
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

    private async Task<OperationResult> ExtractAndAnalyzeMod(string zipPath, GameInstallation gameInstall)
    {
        Console.WriteLine($"ModManager: ExtractAndAnalyzeMod called with zipPath: {zipPath}");
        
        try
        {
            var tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Console.WriteLine($"ModManager: Creating temp extract path: {tempExtractPath}");
            Directory.CreateDirectory(tempExtractPath);

            Console.WriteLine("ModManager: Extracting zip file to temp directory");
            // extract mod to temp directory
            ZipFile.ExtractToDirectory(zipPath, tempExtractPath);

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
                modInfo = ParseManifestToModInfo(manifestJson, "temp");
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
                    modInfo = ParseManifestToModInfo(manifestJson, "temp");
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
                modInfo = await InferModInfo(tempExtractPath);
                Console.WriteLine($"ModManager: Inferred mod info - Name: {modInfo?.Name}, ID: {modInfo?.Id}");
            }

            if (modInfo == null)
            {
                Console.WriteLine("ModManager: Could not determine mod information, cleaning up and returning error");
                Directory.Delete(tempExtractPath, true);
                return OperationResult.ErrorResult("Could not determine mod information", "Invalid mod format");
            }

            Console.WriteLine($"ModManager: Final mod info - Name: '{modInfo.Name}', ID: '{modInfo.Id}', Author: '{modInfo.Author}', Version: '{modInfo.Version}'");

            // cleanup temp directory
            Console.WriteLine("ModManager: Cleaning up temp directory");
            Directory.Delete(tempExtractPath, true);

            return OperationResult.SuccessResult("Mod analyzed successfully", modInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ModManager: ExtractAndAnalyzeMod exception: {ex.Message}");
            Console.WriteLine($"ModManager: ExtractAndAnalyzeMod stack trace: {ex.StackTrace}");
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
            // use cleaned mod name for folder naming (remove whitespace and underscores)
            var folderName = CleanModNameForFolder(modInfo.Name);
            Console.WriteLine($"ModManager: Original mod name: '{modInfo.Name}' -> Cleaned folder name: '{folderName}'");
            
            // update mod ID to match the folder name for consistency
            modInfo.Id = folderName;
            Console.WriteLine($"ModManager: Updated mod ID to match folder name: '{modInfo.Id}'");
            
            var modsBasePath = GetModsDirectoryPath(gameInstall);
            var modPath = Path.Combine(modsBasePath, folderName);
            Console.WriteLine($"ModManager: Full mod installation path: {modPath}");
            
            // create mod directory
            Console.WriteLine("ModManager: Creating mod directory");
            Directory.CreateDirectory(modPath);

            // extract mod files
            Console.WriteLine($"ModManager: Extracting zip file from {zipPath} to {modPath}");
            ZipFile.ExtractToDirectory(zipPath, modPath);

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

            var modPath = Path.Combine(GetModsDirectoryPath(gameInstall), modInfo.Id);
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
}


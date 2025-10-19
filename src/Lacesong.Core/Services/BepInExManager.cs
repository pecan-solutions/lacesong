using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.IO.Compression;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Linq;

namespace Lacesong.Core.Services;

/// <summary>
/// service for managing bepinex installation and configuration
/// </summary>
public class BepInExManager : IBepInExManager
{
    private const string BepInExDownloadUrlTemplate = "https://github.com/BepInEx/BepInEx/releases/download/v{0}/BepInEx_{1}_x64_{0}.zip";
    private const string BepInExCoreDll = "BepInEx/core/BepInEx.Core.dll";
    private const string BepInExLoaderDll = "BepInEx/core/BepInEx.dll";
    private const string GithubReleaseTagUrl = "https://api.github.com/repos/BepInEx/BepInEx/releases/tags/v{0}";
    private const string GithubLatestReleaseUrl = "https://api.github.com/repos/BepInEx/BepInEx/releases/latest";

    private readonly IBepInExVersionCacheService _versionCacheService;

    public BepInExManager(IBepInExVersionCacheService versionCacheService)
    {
        _versionCacheService = versionCacheService;
    }

    /// <summary>
    /// gets the base directory where bepinex should be installed for the given game installation
    /// on windows: directly in the game folder (e.g., "Hollow Knight Silksong/")
    /// on macos: in the folder containing the .app bundle (e.g., "Hollow Knight Silksong/" when app is "Hollow Knight Silksong/Hollow Knight Silksong.app")
    /// </summary>
    private string GetBepInExBaseDirectory(GameInstallation gameInstall)
    {
        // on all platforms, bepinex is installed in the InstallPath directory
        // for windows: this is the folder containing the .exe
        // for macos: this is the folder containing the .app bundle
        // for linux: this is the folder containing the executable
        return gameInstall.InstallPath;
    }

    public async Task<OperationResult> InstallBepInEx(GameInstallation gameInstall, BepInExInstallOptions options)
    {
        try
        {
            // validate game installation
            if (!ValidateGameInstall(gameInstall))
            {
                return OperationResult.ErrorResult("Invalid game installation", "Game installation validation failed");
            }

            // check if already installed
            if (IsBepInExInstalled(gameInstall) && !options.ForceReinstall)
            {
                return OperationResult.SuccessResult("BepInEx is already installed");
            }

            // create backup if requested
            if (options.BackupExisting && IsBepInExInstalled(gameInstall))
            {
                var backupResult = await CreateBackup(gameInstall);
                if (!backupResult.Success)
                {
                    // log the backup failure but continue with installation
                    Console.WriteLine($"Warning: Backup creation failed: {backupResult.Error}. Continuing with installation without backup.");
                }
            }

            // download bepinex
            var downloadResult = await DownloadBepInEx(options.Version, gameInstall);
            if (!downloadResult.Success)
            {
                return OperationResult.ErrorResult($"Failed to download BepInEx: {downloadResult.Error}", "Download failed");
            }

            var tempZipPath = downloadResult.Data as string;
            if (string.IsNullOrEmpty(tempZipPath))
            {
                return OperationResult.ErrorResult("Download result did not contain zip path", "Invalid download result");
            }

            // extract bepinex to the correct base directory
            var baseDir = GetBepInExBaseDirectory(gameInstall);
            var extractResult = await ExtractBepInEx(tempZipPath, baseDir);
            if (!extractResult.Success)
            {
                return OperationResult.ErrorResult($"Failed to extract BepInEx: {extractResult.Error}", "Extraction failed");
            }

            // configure bepinex
            var configResult = await ConfigureBepInEx(gameInstall);
            if (!configResult.Success)
            {
                return OperationResult.ErrorResult($"Failed to configure BepInEx: {configResult.Error}", "Configuration failed");
            }

            // create desktop shortcut if requested
            if (options.CreateDesktopShortcut)
            {
                await CreateDesktopShortcut(gameInstall);
            }

            // cleanup temp files
            try
            {
                File.Delete(tempZipPath);
            }
            catch
            {
                // ignore cleanup errors
            }

            return OperationResult.SuccessResult("BepInEx installed successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "BepInEx installation failed");
        }
    }

    public bool IsBepInExInstalled(GameInstallation gameInstall)
    {
        try
        {
            var baseDir = GetBepInExBaseDirectory(gameInstall);
            var bepinexPath = Path.Combine(baseDir, "BepInEx");
            
            // check if bepinex directory exists
            if (!Directory.Exists(bepinexPath))
                return false;
            
            // check for the loader dll - this is the essential file
            var loaderDllPath = Path.Combine(baseDir, BepInExLoaderDll);
            if (File.Exists(loaderDllPath))
                return true;
            
            // also check for BepInEx.Core.dll as fallback (some versions may have this instead)
            var coreDllPath = Path.Combine(baseDir, BepInExCoreDll);
            if (File.Exists(coreDllPath))
                return true;
            
            // check for alternative doorstop files that might be present (windows)
            var winhttpPath = Path.Combine(baseDir, "winhttp.dll");
            var doorstopConfigPath = Path.Combine(baseDir, "doorstop_config.ini");
            if (File.Exists(winhttpPath) && File.Exists(doorstopConfigPath))
                return true;
            
            // check for macos-specific doorstop files
            var doorstopDylibPath = Path.Combine(baseDir, "libdoorstop.dylib");
            if (File.Exists(doorstopDylibPath))
                return true;
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    public string? GetInstalledBepInExVersion(GameInstallation gameInstall)
    {
        try
        {
            var baseDir = GetBepInExBaseDirectory(gameInstall);
            
            // try to get version from the main BepInEx.dll loader first
            var loaderDllPath = Path.Combine(baseDir, BepInExLoaderDll);
            if (File.Exists(loaderDllPath))
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(loaderDllPath);
                if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                {
                    return versionInfo.FileVersion;
                }
            }

            // fallback to BepInEx.Core.dll if main loader doesn't have version info
            var coreDllPath = Path.Combine(baseDir, BepInExCoreDll);
            if (File.Exists(coreDllPath))
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(coreDllPath);
                if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                {
                    return versionInfo.FileVersion;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public BepInExVersionInfo? GetBepInExVersionInfo(GameInstallation gameInstall)
    {
        try
        {
            var baseDir = GetBepInExBaseDirectory(gameInstall);
            var loaderDllPath = Path.Combine(baseDir, BepInExLoaderDll);
            var coreDllPath = Path.Combine(baseDir, BepInExCoreDll);

            System.Diagnostics.FileVersionInfo? loaderVersionInfo = null;
            System.Diagnostics.FileVersionInfo? coreVersionInfo = null;

            // get version info from main loader
            if (File.Exists(loaderDllPath))
            {
                loaderVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(loaderDllPath);
            }

            // get version info from core dll
            if (File.Exists(coreDllPath))
            {
                coreVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(coreDllPath);
            }

            // prefer loader version, fallback to core version
            var primaryVersionInfo = loaderVersionInfo ?? coreVersionInfo;
            if (primaryVersionInfo == null)
                return null;

            return new BepInExVersionInfo
            {
                FileVersion = primaryVersionInfo.FileVersion,
                ProductVersion = primaryVersionInfo.ProductVersion,
                CompanyName = primaryVersionInfo.CompanyName,
                ProductName = primaryVersionInfo.ProductName,
                Description = primaryVersionInfo.FileDescription,
                LoaderVersion = loaderVersionInfo?.FileVersion,
                CoreVersion = coreVersionInfo?.FileVersion,
                LoaderPath = loaderDllPath,
                CorePath = coreDllPath
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<OperationResult> UninstallBepInEx(GameInstallation gameInstall)
    {
        try
        {
            if (!IsBepInExInstalled(gameInstall))
            {
                return OperationResult.SuccessResult("BepInEx is not installed");
            }

            // create backup before uninstalling
            var backupResult = await CreateBackup(gameInstall);
            if (!backupResult.Success)
            {
                // log the backup failure but continue with uninstallation
                Console.WriteLine($"Warning: Backup creation failed: {backupResult.Error}. Continuing with uninstallation without backup.");
            }

            var baseDir = GetBepInExBaseDirectory(gameInstall);

            // remove bepinex directory
            var bepinexPath = Path.Combine(baseDir, "BepInEx");
            if (Directory.Exists(bepinexPath))
            {
                Directory.Delete(bepinexPath, true);
            }

            // remove winhttp.dll if present
            var winhttpPath = Path.Combine(baseDir, "winhttp.dll");
            if (File.Exists(winhttpPath))
            {
                File.Delete(winhttpPath);
            }

            // remove doorstop config if present
            var doorstopConfigPath = Path.Combine(baseDir, "doorstop_config.ini");
            if (File.Exists(doorstopConfigPath))
            {
                File.Delete(doorstopConfigPath);
            }

            return OperationResult.SuccessResult("BepInEx uninstalled successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "BepInEx uninstallation failed");
        }
    }

    public async Task<BepInExUpdate?> CheckForBepInExUpdates(GameInstallation gameInstall)
    {
        try
        {
            // check if BepInEx is installed
            if (!IsBepInExInstalled(gameInstall))
            {
                return null; // no update available if not installed
            }

            // get current version
            var currentVersion = GetInstalledBepInExVersion(gameInstall);
            if (string.IsNullOrEmpty(currentVersion))
            {
                return null; // cannot determine current version
            }

            // get latest version from cache service
            var latestVersionInfo = await _versionCacheService.GetLatestVersionInfoAsync();
            if (latestVersionInfo == null || string.IsNullOrEmpty(latestVersionInfo.FileVersion))
            {
                return null; // failed to get latest version
            }

            var latestVersion = latestVersionInfo.FileVersion;

            // remove 'v' prefix for comparison
            var cleanLatestVersion = latestVersion.TrimStart('v');
            var cleanCurrentVersion = currentVersion.TrimStart('v');

            // compare versions
            if (!IsNewerVersion(cleanLatestVersion, cleanCurrentVersion))
            {
                return null; // no update available
            }

            // get download URL for the platform
            var downloadUrl = await ResolveAssetUrl(cleanLatestVersion, gameInstall);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                return null; // cannot resolve download URL
            }

            return new BepInExUpdate
            {
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                DownloadUrl = downloadUrl,
                ReleaseNotes = latestVersionInfo.Description ?? string.Empty,
                PublishedAt = DateTime.UtcNow, // we could store this in the cache if needed
                IsPrerelease = false, // we could determine this from cache if needed
                FileSize = 0L // we could store this in the cache if needed
            };
        }
        catch (Exception ex)
        {
            // log error but don't throw - this is a background check
            Console.WriteLine($"Error checking for BepInEx updates: {ex.Message}");
            return null;
        }
    }

    public async Task<OperationResult> UpdateBepInEx(GameInstallation gameInstall, BepInExUpdateOptions? options = null, IProgress<double>? progress = null)
    {
        try
        {
            options ??= new BepInExUpdateOptions();
            progress?.Report(0.1);

            // check for available update
            var update = await CheckForBepInExUpdates(gameInstall);
            if (update == null)
            {
                return OperationResult.ErrorResult("No BepInEx update available", "Update check failed");
            }

            progress?.Report(0.2);

            // create backup if requested
            if (options.BackupExisting)
            {
                var backupResult = await CreateBackup(gameInstall);
                if (!backupResult.Success)
                {
                    // log the backup failure but continue with update
                    Console.WriteLine($"Warning: Backup creation failed: {backupResult.Error}. Continuing with update without backup.");
                }
            }

            progress?.Report(0.3);

            // download the update
            var downloadResult = await DownloadBepInEx(update.LatestVersion.TrimStart('v'), gameInstall, progress);
            if (!downloadResult.Success)
            {
                return OperationResult.ErrorResult($"Failed to download BepInEx update: {downloadResult.Error}", "Download failed");
            }

            progress?.Report(0.7);

            var tempZipPath = downloadResult.Data as string;
            if (string.IsNullOrEmpty(tempZipPath))
            {
                return OperationResult.ErrorResult("Download result did not contain zip path", "Invalid download result");
            }

            // extract the update
            var baseDir = GetBepInExBaseDirectory(gameInstall);
            var extractResult = await ExtractBepInEx(tempZipPath, baseDir, progress);
            if (!extractResult.Success)
            {
                return OperationResult.ErrorResult($"Failed to extract BepInEx update: {extractResult.Error}", "Extraction failed");
            }

            progress?.Report(0.9);

            // configure BepInEx
            var configResult = await ConfigureBepInEx(gameInstall);
            if (!configResult.Success)
            {
                return OperationResult.ErrorResult($"Failed to configure BepInEx: {configResult.Error}", "Configuration failed");
            }

            // cleanup temp files
            try
            {
                File.Delete(tempZipPath);
            }
            catch
            {
                // ignore cleanup errors
            }

            progress?.Report(1.0);

            return OperationResult.SuccessResult($"BepInEx updated successfully from {update.CurrentVersion} to {update.LatestVersion}");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "BepInEx update failed");
        }
    }

    private bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        try
        {
            // simple version comparison - assumes semantic versioning
            var latestParts = latestVersion.Split('.').Select(int.Parse).ToArray();
            var currentParts = currentVersion.Split('.').Select(int.Parse).ToArray();

            // pad arrays to same length
            var maxLength = Math.Max(latestParts.Length, currentParts.Length);
            Array.Resize(ref latestParts, maxLength);
            Array.Resize(ref currentParts, maxLength);

            for (int i = 0; i < maxLength; i++)
            {
                if (latestParts[i] > currentParts[i])
                    return true;
                if (latestParts[i] < currentParts[i])
                    return false;
            }

            return false; // versions are equal
        }
        catch
        {
            // if version parsing fails, assume no update available
            return false;
        }
    }

    private bool ValidateGameInstall(GameInstallation gameInstall)
    {
        if (string.IsNullOrEmpty(gameInstall.InstallPath) || string.IsNullOrEmpty(gameInstall.Executable))
            return false;

        var executablePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
        return File.Exists(executablePath);
    }

    private static (string os, string arch) DetermineTarget(GameInstallation gi)
    {
        var exePath = Path.Combine(gi.InstallPath, gi.Executable);
        var exeType = ExecutableTypeDetector.GetExecutableType(exePath);

        var os = exeType switch
        {
            ExecutableType.Windows => "win",
            ExecutableType.macOS => "macos",
            ExecutableType.Unix => "linux",
            _ => "win"
        };

        // use detected architecture from game installation, or detect it if not set
        var architecture = gi.Architecture != Models.Architecture.X64 ? gi.Architecture : ExecutableArchitectureDetector.DetectArchitecture(exePath);
        var arch = ExecutableArchitectureDetector.GetArchitectureString(architecture);

        return (os, arch);
    }

    private async Task<string?> ResolveAssetUrl(string version, GameInstallation gi)
    {
        try
        {
            var tagUrl = string.Format(GithubReleaseTagUrl, version);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Lacesong-ModManager/1.0.0");

            var response = await client.GetAsync(tagUrl);
            if (!response.IsSuccessStatusCode) return null;

            using var responseContent = response.Content;
            using var doc = JsonDocument.Parse(await responseContent.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("assets", out var assetsElement)) return null;

            var (os, arch) = DetermineTarget(gi);
            var expectedName = $"BepInEx_{os}_{arch}_{version}.zip";

            foreach (var asset in assetsElement.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameEl)) continue;
                var name = nameEl.GetString();
                if (name == expectedName && asset.TryGetProperty("browser_download_url", out var urlEl))
                {
                    return urlEl.GetString();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<OperationResult> DownloadBepInEx(string version, GameInstallation gi, IProgress<double>? progress = null)
    {
        try
        {
            var assetUrl = await ResolveAssetUrl(version, gi);
            if (string.IsNullOrEmpty(assetUrl))
            {
                return OperationResult.ErrorResult("Could not resolve BepInEx asset URL for this platform.");
            }

            var tempPath = Path.GetTempFileName();
            var tempZip = Path.ChangeExtension(tempPath, ".zip");
            File.Delete(tempPath);

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await client.GetAsync(assetUrl);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;
            
            using var responseContent = response.Content;
            using var stream = await responseContent.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write);
            
            var buffer = new byte[8192];
            int bytesRead;
            
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;
                
                if (totalBytes > 0)
                {
                    var downloadProgress = (double)downloadedBytes / totalBytes * 0.4; // 40% of total progress
                    progress?.Report(0.3 + downloadProgress);
                }
            }

            return OperationResult.SuccessResult("Downloaded BepInEx", tempZip);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to download BepInEx");
        }
    }

    private async Task<OperationResult> ExtractBepInEx(string zipPath, string gamePath, IProgress<double>? progress = null)
    {
        try
        {
            var tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempExtractPath);

            // extract to temp directory first
            ZipFile.ExtractToDirectory(zipPath, tempExtractPath);

            // get all files and directories from the extracted content
            var extractedFiles = Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories);
            var extractedDirs = Directory.GetDirectories(tempExtractPath, "*", SearchOption.AllDirectories);

            // first, remove existing BepInEx files to ensure clean replacement
            var existingBepInExPath = Path.Combine(gamePath, "BepInEx");
            if (Directory.Exists(existingBepInExPath))
            {
                Directory.Delete(existingBepInExPath, true);
            }

            // remove other BepInEx-related files that might exist
            var bepinexFiles = new[] { "winhttp.dll", "doorstop_config.ini", "libdoorstop.dylib", "run_bepinex.sh" };
            foreach (var file in bepinexFiles)
            {
                var filePath = Path.Combine(gamePath, file);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch
                    {
                        // ignore if file is in use or can't be deleted
                    }
                }
            }

            // create directories first
            foreach (var dir in extractedDirs)
            {
                var relativePath = Path.GetRelativePath(tempExtractPath, dir);
                var targetPath = Path.Combine(gamePath, relativePath);
                Directory.CreateDirectory(targetPath);
            }

            // copy files, ensuring they replace existing ones
            var totalFiles = extractedFiles.Length;
            for (int i = 0; i < extractedFiles.Length; i++)
            {
                var file = extractedFiles[i];
                var relativePath = Path.GetRelativePath(tempExtractPath, file);
                var targetPath = Path.Combine(gamePath, relativePath);
                
                var targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // copy with overwrite to replace existing files
                File.Copy(file, targetPath, true);
                
                // report progress for extraction (20% of total progress)
                var extractionProgress = (double)(i + 1) / totalFiles * 0.2;
                progress?.Report(0.7 + extractionProgress);
            }

            // cleanup temp directory
            Directory.Delete(tempExtractPath, true);

            return OperationResult.SuccessResult("BepInEx extracted successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to extract BepInEx");
        }
    }

    private async Task<OperationResult> ConfigureBepInEx(GameInstallation gameInstall)
    {
        try
        {
            var baseDir = GetBepInExBaseDirectory(gameInstall);
            var configPath = Path.Combine(baseDir, "BepInEx", "config", "BepInEx.cfg");
            var configDir = Path.GetDirectoryName(configPath);
            
            if (!string.IsNullOrEmpty(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            // create basic bepinex configuration
            var config = $@"[Logging.Console]
Enabled = true
LogLevel = Info

[Logging.File]
Enabled = true
LogLevel = Info
LogFileName = LogOutput.log

[Logging.UnityLogListener]
Enabled = true
LogLevel = Info

[Chainloader]
SkipAssemblyScan = false
";

            await File.WriteAllTextAsync(configPath, config);

            // patch run_bepinex.sh if required based on target executable type
            try
            {
                var exePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
                var exeType = ExecutableTypeDetector.GetExecutableType(exePath);
                if (exeType == ExecutableType.macOS || exeType == ExecutableType.Unix)
                {
                    var scriptPath = Path.Combine(baseDir, "run_bepinex.sh");
                    if (File.Exists(scriptPath))
                    {
                        var lines = await File.ReadAllLinesAsync(scriptPath);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].TrimStart().StartsWith("executable_name="))
                            {
                                var replacement = exeType == ExecutableType.macOS
                                    ? "executable_name=\"Hollow Knight Silksong.app\""
                                    : "executable_name=\"Hollow Knight Silksong\"";
                                lines[i] = replacement;
                                break;
                            }
                        }
                        await File.WriteAllLinesAsync(scriptPath, lines);
                        // ensure script is executable on unix systems
                        try
                        {
                            if (!OperatingSystem.IsWindows())
                            {
                                System.Diagnostics.Process.Start("chmod", $"+x \"{scriptPath}\"")?.WaitForExit();
                            }
                        }
                        catch { /* ignore chmod errors */ }
                    }
                }
            }
            catch { /* ignore script patch errors */ }
 
            return OperationResult.SuccessResult("BepInEx configured successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to configure BepInEx");
        }
    }

    private async Task<OperationResult> CreateBackup(GameInstallation gameInstall)
    {
        try
        {
            var baseDir = GetBepInExBaseDirectory(gameInstall);
            var bepinexPath = Path.Combine(baseDir, "BepInEx");
            
            // check if BepInEx directory exists - if not, no backup needed
            if (!Directory.Exists(bepinexPath))
            {
                return OperationResult.SuccessResult("No existing BepInEx installation to backup", null);
            }

            // ensure we can write to the game directory
            var backupDir = Path.Combine(baseDir, "BepInEx", "backups");
            try
            {
                Directory.CreateDirectory(backupDir);
            }
            catch (Exception ex)
            {
                // if we can't create backup directory, try using temp directory instead
                var tempBackupDir = Path.Combine(Path.GetTempPath(), "lacesong_bepinex_backups");
                try
                {
                    Directory.CreateDirectory(tempBackupDir);
                    backupDir = tempBackupDir;
                }
                catch (Exception tempEx)
                {
                    // if we can't even create temp backup directory, fail gracefully
                    return OperationResult.ErrorResult($"Unable to create backup directory: {ex.Message}. Temp directory also failed: {tempEx.Message}", "Backup directory creation failed");
                }
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupDir, $"bepinex_backup_{timestamp}.zip");

            // create the backup archive
            ZipFile.CreateFromDirectory(bepinexPath, backupPath);

            // cleanup old backups to prevent storage bloat
            await CleanupOldBackups(backupDir);

            return OperationResult.SuccessResult("Backup created successfully", backupPath);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to create backup");
        }
    }

    public async Task<OperationResult> CleanupBepInExBackups(GameInstallation gameInstall, int maxBackups = 5, int maxAgeDays = 30)
    {
        try
        {
            var baseDir = GetBepInExBaseDirectory(gameInstall);
            var backupDir = Path.Combine(baseDir, "BepInEx", "backups");
            
            if (!Directory.Exists(backupDir))
            {
                return OperationResult.SuccessResult("No backup directory found - nothing to clean up");
            }

            var backupFiles = Directory.GetFiles(backupDir, "bepinex_backup_*.zip")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            if (backupFiles.Count == 0)
            {
                return OperationResult.SuccessResult("No backup files found - nothing to clean up");
            }

            var filesToDelete = new List<FileInfo>();
            var totalSizeToDelete = 0L;

            // find files to delete based on count limit
            if (backupFiles.Count > maxBackups)
            {
                var excessFiles = backupFiles.Skip(maxBackups);
                filesToDelete.AddRange(excessFiles);
            }

            // find files to delete based on age limit
            var cutoffDate = DateTime.Now.AddDays(-maxAgeDays);
            var oldFiles = backupFiles.Where(f => f.CreationTime < cutoffDate).ToList();
            
            foreach (var file in oldFiles)
            {
                if (!filesToDelete.Contains(file))
                {
                    filesToDelete.Add(file);
                }
            }

            // delete files
            var deletedCount = 0;
            foreach (var file in filesToDelete)
            {
                try
                {
                    totalSizeToDelete += file.Length;
                    File.Delete(file.FullName);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete backup {file.Name}: {ex.Message}");
                }
            }

            var remainingCount = backupFiles.Count - deletedCount;
            var sizeFreedMB = totalSizeToDelete / (1024.0 * 1024.0);

            return OperationResult.SuccessResult(
                $"Backup cleanup completed. Deleted {deletedCount} files, freed {sizeFreedMB:F1} MB. {remainingCount} backups remaining.",
                new BackupCleanupResult 
                { 
                    DeletedCount = deletedCount, 
                    SizeFreedMB = sizeFreedMB, 
                    RemainingCount = remainingCount 
                }
            );
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Backup cleanup failed");
        }
    }

    private async Task CleanupOldBackups(string backupDir)
    {
        try
        {
            if (!Directory.Exists(backupDir))
                return;

            var backupFiles = Directory.GetFiles(backupDir, "bepinex_backup_*.zip")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            // keep only the 5 most recent backups
            const int maxBackups = 5;
            if (backupFiles.Count > maxBackups)
            {
                var filesToDelete = backupFiles.Skip(maxBackups);
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file.FullName);
                        Console.WriteLine($"Cleaned up old backup: {file.Name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete old backup {file.Name}: {ex.Message}");
                    }
                }
            }

            // also clean up backups older than 30 days
            var cutoffDate = DateTime.Now.AddDays(-30);
            var oldBackups = backupFiles.Where(f => f.CreationTime < cutoffDate).ToList();
            
            foreach (var file in oldBackups)
            {
                try
                {
                    File.Delete(file.FullName);
                    Console.WriteLine($"Cleaned up old backup (30+ days): {file.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete old backup {file.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during backup cleanup: {ex.Message}");
        }
    }

    private async Task CreateDesktopShortcut(GameInstallation gameInstall)
    {
        try
        {
            // this would create a desktop shortcut on windows
            // implementation depends on the target platform
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var shortcutPath = Path.Combine(desktopPath, $"{gameInstall.Name} (BepInEx).lnk");
                
                // create shortcut using windows shell
                // this is a simplified implementation - in production you'd use proper shortcut creation
                var targetPath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
                var workingDir = gameInstall.InstallPath;
                
                // note: actual shortcut creation would require additional windows-specific code
                // for now, we'll just create a batch file as a workaround
                var batchPath = Path.ChangeExtension(shortcutPath, ".bat");
                var batchContent = $@"@echo off
cd /d ""{workingDir}""
start """" ""{targetPath}""
";
                await File.WriteAllTextAsync(batchPath, batchContent);
            }
        }
        catch (Exception ex)
        {
            // ignore shortcut creation errors
            Console.WriteLine($"Failed to create desktop shortcut: {ex.Message}");
        }
    }
}

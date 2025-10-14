using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.IO.Compression;

namespace Lacesong.Core.Services;

/// <summary>
/// service for managing bepinex installation and configuration
/// </summary>
public class BepInExManager : IBepInExManager
{
    private const string BepInExDownloadUrlTemplate = "https://github.com/BepInEx/BepInEx/releases/download/v{0}/BepInEx_{1}_x64_{0}.zip";
    private const string BepInExCoreDll = "BepInEx/core/BepInEx.Core.dll";
    private const string BepInExLoaderDll = "BepInEx/core/BepInEx.dll";

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
                    return OperationResult.ErrorResult($"Failed to create backup: {backupResult.Error}", "Backup creation failed");
                }
            }

            // download bepinex
            var downloadResult = await DownloadBepInEx(options.Version);
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
                return OperationResult.ErrorResult($"Failed to create backup: {backupResult.Error}", "Backup creation failed");
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

    private bool ValidateGameInstall(GameInstallation gameInstall)
    {
        if (string.IsNullOrEmpty(gameInstall.InstallPath) || string.IsNullOrEmpty(gameInstall.Executable))
            return false;

        var executablePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
        return File.Exists(executablePath);
    }

    private async Task<OperationResult> DownloadBepInEx(string version)
    {
        try
        {
            // determine platform for download url
            var platform = GetPlatformName();
            var downloadUrl = string.Format(BepInExDownloadUrlTemplate, version, platform);
            var tempPath = Path.GetTempFileName();
            var tempZipPath = Path.ChangeExtension(tempPath, ".zip");
            File.Delete(tempPath);

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            var response = await httpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(tempZipPath, content);

            return OperationResult.SuccessResult("BepInEx downloaded successfully", tempZipPath);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to download BepInEx");
        }
    }

    private string GetPlatformName()
    {
        if (OperatingSystem.IsWindows())
            return "win";
        if (OperatingSystem.IsLinux())
            return "linux";
        if (OperatingSystem.IsMacOS())
            return "macos";
        
        // default to windows if platform detection fails
        return "win";
    }

    private async Task<OperationResult> ExtractBepInEx(string zipPath, string gamePath)
    {
        try
        {
            var tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempExtractPath);

            // extract to temp directory first
            ZipFile.ExtractToDirectory(zipPath, tempExtractPath);

            // move files to game directory atomically
            var extractedFiles = Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories);
            var extractedDirs = Directory.GetDirectories(tempExtractPath, "*", SearchOption.AllDirectories);

            // create directories first
            foreach (var dir in extractedDirs)
            {
                var relativePath = Path.GetRelativePath(tempExtractPath, dir);
                var targetPath = Path.Combine(gamePath, relativePath);
                Directory.CreateDirectory(targetPath);
            }

            // copy files
            foreach (var file in extractedFiles)
            {
                var relativePath = Path.GetRelativePath(tempExtractPath, file);
                var targetPath = Path.Combine(gamePath, relativePath);
                
                // ensure target directory exists
                var targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.Copy(file, targetPath, true);
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
            var backupDir = Path.Combine(baseDir, "BepInEx", "backups");
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupDir, $"bepinex_backup_{timestamp}.zip");

            var bepinexPath = Path.Combine(baseDir, "BepInEx");
            if (Directory.Exists(bepinexPath))
            {
                ZipFile.CreateFromDirectory(bepinexPath, backupPath);
            }

            return OperationResult.SuccessResult("Backup created successfully", backupPath);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to create backup");
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

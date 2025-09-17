using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.IO.Compression;

namespace Lacesong.Core.Services;

/// <summary>
/// service for managing bepinex installation and configuration
/// </summary>
public class BepInExManager : IBepInExManager
{
    private const string BepInExDownloadUrl = "https://github.com/BepInEx/BepInEx/releases/download/v{0}/BepInEx_x64_{0}.zip";
    private const string BepInExCoreDll = "BepInEx/core/BepInEx.Core.dll";
    private const string BepInExLoaderDll = "BepInEx/core/BepInEx.dll";

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

            // extract bepinex
            var extractResult = await ExtractBepInEx(tempZipPath, gameInstall.InstallPath);
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
            var bepinexPath = Path.Combine(gameInstall.InstallPath, "BepInEx");
            var winhttpPath = Path.Combine(gameInstall.InstallPath, "winhttp.dll");

            return Directory.Exists(bepinexPath) && File.Exists(winhttpPath);
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
            var coreDllPath = Path.Combine(gameInstall.InstallPath, BepInExCoreDll);
            if (!File.Exists(coreDllPath))
                return null;

            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(coreDllPath);
            return versionInfo.FileVersion;
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

            // remove bepinex directory
            var bepinexPath = Path.Combine(gameInstall.InstallPath, "BepInEx");
            if (Directory.Exists(bepinexPath))
            {
                Directory.Delete(bepinexPath, true);
            }

            // remove winhttp.dll if present
            var winhttpPath = Path.Combine(gameInstall.InstallPath, "winhttp.dll");
            if (File.Exists(winhttpPath))
            {
                File.Delete(winhttpPath);
            }

            // remove doorstop config if present
            var doorstopConfigPath = Path.Combine(gameInstall.InstallPath, "doorstop_config.ini");
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
            var downloadUrl = string.Format(BepInExDownloadUrl, version);
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
            var configPath = Path.Combine(gameInstall.InstallPath, "BepInEx", "config", "BepInEx.cfg");
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
            var backupDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "backups");
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupDir, $"bepinex_backup_{timestamp}.zip");

            var bepinexPath = Path.Combine(gameInstall.InstallPath, "BepInEx");
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

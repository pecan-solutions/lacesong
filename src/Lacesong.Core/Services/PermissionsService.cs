using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Security.Principal;

namespace Lacesong.Core.Services;

/// <summary>
/// service for user permissions and elevation management
/// </summary>
public class PermissionsService : IPermissionsService
{
    public async Task<UserPermissions> CheckPermissions(GameInstallation gameInstall)
    {
        var permissions = new UserPermissions();

        try
        {
            // check if current user is elevated/administrator
            permissions.IsElevated = IsCurrentUserElevated();

            // check write permissions to game directory
            permissions.CanWriteToGameDirectory = await CanWriteToDirectory(gameInstall.InstallPath);

            // check system file creation permissions
            permissions.CanCreateSystemFiles = await CanCreateSystemFiles();

            // check registry modification permissions (Windows only)
            permissions.CanModifyRegistry = await CanModifyRegistry();

            // determine if elevation is required
            permissions.RequiresElevation = DetermineElevationRequirement(permissions, gameInstall);

            if (permissions.RequiresElevation)
            {
                permissions.ElevationReason = GetElevationReason(permissions, gameInstall);
            }

            return permissions;
        }
        catch (Exception ex)
        {
            // return permissions with error state
            return new UserPermissions
            {
                IsElevated = false,
                CanWriteToGameDirectory = false,
                CanCreateSystemFiles = false,
                CanModifyRegistry = false,
                RequiresElevation = true,
                ElevationReason = $"Permission check failed: {ex.Message}"
            };
        }
    }

    public async Task<OperationResult> RequestElevation(string reason)
    {
        try
        {
            // check if already elevated
            if (IsCurrentUserElevated())
            {
                return OperationResult.SuccessResult("User is already elevated");
            }

            // on Windows, try to restart with elevation
            if (OperatingSystem.IsWindows())
            {
                return await RequestWindowsElevation(reason);
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                return await RequestUnixElevation(reason);
            }

            return OperationResult.ErrorResult("Elevation not supported on this platform", "Platform not supported");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Elevation request failed");
        }
    }

    public async Task<bool> RequiresElevation(string operation, GameInstallation gameInstall)
    {
        try
        {
            var permissions = await CheckPermissions(gameInstall);

            return operation.ToLowerInvariant() switch
            {
                "install-bepinex" => !permissions.CanWriteToGameDirectory,
                "uninstall-bepinex" => !permissions.CanWriteToGameDirectory,
                "install-mod" => !permissions.CanWriteToGameDirectory,
                "uninstall-mod" => !permissions.CanWriteToGameDirectory,
                "create-backup" => !permissions.CanWriteToGameDirectory,
                "restore-backup" => !permissions.CanWriteToGameDirectory,
                "create-desktop-shortcut" => !permissions.CanCreateSystemFiles,
                "modify-registry" => !permissions.CanModifyRegistry,
                _ => permissions.RequiresElevation
            };
        }
        catch
        {
            return true; // if we can't determine, assume elevation is required
        }
    }

    private bool IsCurrentUserElevated()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // check if running as root
                return Environment.UserName == "root" || Environment.GetEnvironmentVariable("SUDO_USER") != null;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CanWriteToDirectory(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                // try to create the directory to test permissions
                try
                {
                    Directory.CreateDirectory(directoryPath);
                    Directory.Delete(directoryPath);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // test write permission by creating a temporary file
            var testFile = Path.Combine(directoryPath, $"test_write_{Guid.NewGuid()}.tmp");
            try
            {
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CanCreateSystemFiles()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // test creating a file in system directory
                var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var testFile = Path.Combine(systemDir, $"test_system_{Guid.NewGuid()}.tmp");
                
                try
                {
                    await File.WriteAllTextAsync(testFile, "test");
                    File.Delete(testFile);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // test creating a file in /tmp (should always work)
                var testFile = Path.Combine("/tmp", $"test_system_{Guid.NewGuid()}.tmp");
                try
                {
                    await File.WriteAllTextAsync(testFile, "test");
                    File.Delete(testFile);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CanModifyRegistry()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return false; // registry only exists on Windows
            }

            // test registry write permission
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software", true);
                if (key != null)
                {
                    var testKey = key.CreateSubKey($"LacesongTest_{Guid.NewGuid()}");
                    testKey?.Close();
                    key.DeleteSubKey(testKey?.Name ?? "", false);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool DetermineElevationRequirement(UserPermissions permissions, GameInstallation gameInstall)
    {
        // require elevation if any critical permissions are missing
        return !permissions.CanWriteToGameDirectory || 
               !permissions.CanCreateSystemFiles || 
               (!permissions.IsElevated && NeedsAdminAccess(gameInstall));
    }

    private bool NeedsAdminAccess(GameInstallation gameInstall)
    {
        try
        {
            // check if game is installed in a protected location
            var installPath = gameInstall.InstallPath.ToLowerInvariant();

            if (OperatingSystem.IsWindows())
            {
                // common protected locations on Windows
                var protectedPaths = new[]
                {
                    @"c:\program files\",
                    @"c:\program files (x86)\",
                    @"c:\windows\",
                    @"c:\system32\"
                };

                return protectedPaths.Any(path => installPath.StartsWith(path));
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // common protected locations on Unix-like systems
                var protectedPaths = new[]
                {
                    "/usr/",
                    "/opt/",
                    "/system/",
                    "/bin/",
                    "/sbin/"
                };

                return protectedPaths.Any(path => installPath.StartsWith(path));
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private string GetElevationReason(UserPermissions permissions, GameInstallation gameInstall)
    {
        var reasons = new List<string>();

        if (!permissions.CanWriteToGameDirectory)
        {
            reasons.Add("Cannot write to game directory");
        }

        if (!permissions.CanCreateSystemFiles)
        {
            reasons.Add("Cannot create system files");
        }

        if (NeedsAdminAccess(gameInstall))
        {
            reasons.Add("Game is installed in a protected location");
        }

        return string.Join(", ", reasons);
    }

    private async Task<OperationResult> RequestWindowsElevation(string reason)
    {
        try
        {
            // get current executable path
            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
            {
                return OperationResult.ErrorResult("Cannot determine current executable path", "Elevation failed");
            }

            // prepare elevation arguments
            var arguments = $"--elevated --reason \"{reason}\"";
            
            // create process start info for elevation
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = currentExe,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas", // this triggers UAC on Windows
                CreateNoWindow = false
            };

            // start elevated process
            var process = System.Diagnostics.Process.Start(startInfo);
            
            if (process == null)
            {
                return OperationResult.ErrorResult("Failed to start elevated process", "Elevation failed");
            }

            // wait for the elevated process to complete
            await process.WaitForExitAsync();

            return process.ExitCode == 0
                ? OperationResult.SuccessResult("Elevation successful")
                : OperationResult.ErrorResult($"Elevated process exited with code {process.ExitCode}", "Elevation failed");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Windows elevation failed");
        }
    }

    private async Task<OperationResult> RequestUnixElevation(string reason)
    {
        try
        {
            // on Unix systems, we typically use sudo
            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
            {
                return OperationResult.ErrorResult("Cannot determine current executable path", "Elevation failed");
            }

            // check if sudo is available
            var sudoPath = await FindSudoPath();
            if (string.IsNullOrEmpty(sudoPath))
            {
                return OperationResult.ErrorResult("sudo not available", "Elevation not possible");
            }

            // prepare sudo command
            var arguments = $"-S {currentExe} --elevated --reason \"{reason}\"";
            
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = sudoPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                return OperationResult.ErrorResult("Failed to start sudo process", "Elevation failed");
            }

            // note: in a real implementation, you'd need to handle password input
            // for now, we'll assume the user has already provided credentials
            
            await process.WaitForExitAsync();

            return process.ExitCode == 0
                ? OperationResult.SuccessResult("Elevation successful")
                : OperationResult.ErrorResult($"sudo process exited with code {process.ExitCode}", "Elevation failed");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Unix elevation failed");
        }
    }

    private async Task<string> FindSudoPath()
    {
        try
        {
            var commonPaths = new[]
            {
                "/usr/bin/sudo",
                "/bin/sudo",
                "/usr/local/bin/sudo"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // try to find sudo in PATH
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "which",
                Arguments = "sudo",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var sudoPath = output.Trim();
                    if (!string.IsNullOrEmpty(sudoPath) && File.Exists(sudoPath))
                    {
                        return sudoPath;
                    }
                }
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

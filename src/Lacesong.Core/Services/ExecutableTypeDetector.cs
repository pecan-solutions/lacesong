using System.Runtime.InteropServices;
using Lacesong.Core.Models;

namespace Lacesong.Core.Services;

/// <summary>
/// service for detecting executable type based on file extension rather than current platform
/// </summary>
public static class ExecutableTypeDetector
{
    /// <summary>
    /// determines the executable type based on file extension
    /// </summary>
    public static ExecutableType GetExecutableType(string executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
            return ExecutableType.Unknown;

        var extension = Path.GetExtension(executablePath).ToLowerInvariant();
        
        return extension switch
        {
            ".exe" => ExecutableType.Windows,
            ".app" => ExecutableType.macOS,
            "" => ExecutableType.Unix, // no extension typically means unix executable
            _ => ExecutableType.Unknown
        };
    }

    /// <summary>
    /// determines if the executable should be treated as a macOS app bundle
    /// </summary>
    public static bool IsMacOSAppBundle(string executablePath)
    {
        return GetExecutableType(executablePath) == ExecutableType.macOS;
    }

    /// <summary>
    /// determines if the executable should be treated as a Windows executable
    /// </summary>
    public static bool IsWindowsExecutable(string executablePath)
    {
        return GetExecutableType(executablePath) == ExecutableType.Windows;
    }

    /// <summary>
    /// determines if the executable should be treated as a Unix executable
    /// </summary>
    public static bool IsUnixExecutable(string executablePath)
    {
        return GetExecutableType(executablePath) == ExecutableType.Unix;
    }

    /// <summary>
    /// gets the appropriate executable name for the target executable type
    /// </summary>
    public static string GetExecutableName(string baseName, ExecutableType targetType)
    {
        return targetType switch
        {
            ExecutableType.Windows => $"{baseName}.exe",
            ExecutableType.macOS => $"{baseName}.app",
            ExecutableType.Unix => baseName,
            _ => baseName
        };
    }

    /// <summary>
    /// checks if a path points to a valid executable for the detected executable type
    /// </summary>
    public static bool IsValidExecutable(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        var executableType = GetExecutableType(filePath);
        
        return executableType switch
        {
            ExecutableType.Windows => Path.GetExtension(filePath).ToLowerInvariant() == ".exe",
            ExecutableType.macOS => Directory.Exists(filePath) && filePath.EndsWith(".app"),
            ExecutableType.Unix => IsPosixExecutable(filePath),
            _ => false
        };
    }

    /// <summary>
    /// gets the actual executable path within an app bundle for macOS
    /// </summary>
    public static string GetAppBundleExecutablePath(string appBundlePath)
    {
        if (!IsMacOSAppBundle(appBundlePath))
            return appBundlePath;

        var baseName = Path.GetFileNameWithoutExtension(appBundlePath);
        return Path.Combine(appBundlePath, "Contents", "MacOS", baseName);
    }

    /// <summary>
    /// gets the mods directory path based on executable type rather than current platform
    /// </summary>
    public static string GetModsDirectoryPath(GameInstallation gameInstall)
    {
        var executableType = GetExecutableType(Path.Combine(gameInstall.InstallPath, gameInstall.Executable));
        
        // for macos app bundles, unity games use .app bundles with data stored at Contents/Resources/Data/
        if (executableType == ExecutableType.macOS)
        {
            var appBundlePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
            if (Directory.Exists(appBundlePath))
            {
                // macos unity games store data in Contents/Resources/Data/
                return Path.Combine(appBundlePath, "Contents", "Resources", "Data", "Managed", "Mods");
            }
        }
        
        // fallback to standard path for windows, linux, or if .app bundle not found
        return Path.Combine(gameInstall.InstallPath, gameInstall.ModDirectory);
    }

    /// <summary>
    /// determines if run_bepinex.sh script should be used based on executable type
    /// </summary>
    public static bool ShouldUseRunBepInExScript(string executablePath)
    {
        var executableType = GetExecutableType(executablePath);
        // use script for unix executables (including macos when not using .app bundles)
        return executableType == ExecutableType.Unix;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "access")]
    private static extern int access(string pathname, int mode);

    private static bool IsPosixExecutable(string filePath)
    {
        // X_OK check for execute permission
        const int X_OK = 1;
        return access(filePath, X_OK) == 0;
    }
}

/// <summary>
/// represents executable types based on file extension
/// </summary>
public enum ExecutableType
{
    Unknown,
    Windows,  // .exe files
    macOS,    // .app bundles
    Unix      // no extension or other unix executables
}

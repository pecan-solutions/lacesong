using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Lacesong.Core.Services;

/// <summary>
/// service for detecting the current platform and providing platform-specific paths
/// </summary>
public static class PlatformDetector
{
    public static Platform CurrentPlatform { get; }
    public static bool IsWindows { get; }
    public static bool IsMacOS { get; }
    public static bool IsLinux { get; }

    static PlatformDetector()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            CurrentPlatform = Platform.Windows;
            IsWindows = true;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            CurrentPlatform = Platform.macOS;
            IsMacOS = true;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            CurrentPlatform = Platform.Linux;
            IsLinux = true;
        }
        else
        {
            CurrentPlatform = Platform.Unknown;
        }
    }

    /// <summary>
    /// gets steam installation paths for the current platform
    /// </summary>
    public static List<string> GetSteamPaths()
    {
        var paths = new List<string>();

        switch (CurrentPlatform)
        {
            case Platform.Windows:
                paths.AddRange(GetWindowsSteamPaths());
                break;
            case Platform.macOS:
                paths.AddRange(GetMacOSSteamPaths());
                break;
            case Platform.Linux:
                paths.AddRange(GetLinuxSteamPaths());
                break;
        }

        return paths.Where(Directory.Exists).ToList();
    }

    /// <summary>
    /// gets gog galaxy installation paths for the current platform
    /// </summary>
    public static List<string> GetGogPaths()
    {
        var paths = new List<string>();

        switch (CurrentPlatform)
        {
            case Platform.Windows:
                paths.AddRange(GetWindowsGogPaths());
                break;
            case Platform.macOS:
                paths.AddRange(GetMacOSGogPaths());
                break;
            case Platform.Linux:
                paths.AddRange(GetLinuxGogPaths());
                break;
        }

        return paths.Where(Directory.Exists).ToList();
    }

    /// <summary>
    /// gets epic games launcher paths for the current platform
    /// </summary>
    public static List<string> GetEpicPaths()
    {
        var paths = new List<string>();

        switch (CurrentPlatform)
        {
            case Platform.Windows:
                paths.AddRange(GetWindowsEpicPaths());
                break;
            case Platform.macOS:
                paths.AddRange(GetMacOSEpicPaths());
                break;
            case Platform.Linux:
                paths.AddRange(GetLinuxEpicPaths());
                break;
        }

        return paths.Where(Directory.Exists).ToList();
    }

    /// <summary>
    /// gets xbox game pass paths for the current platform
    /// </summary>
    public static List<string> GetXboxPaths()
    {
        var paths = new List<string>();

        switch (CurrentPlatform)
        {
            case Platform.Windows:
                paths.AddRange(GetWindowsXboxPaths());
                break;
            case Platform.macOS:
                // xbox game pass is not available on macos
                break;
            case Platform.Linux:
                // xbox game pass is not available on linux
                break;
        }

        return paths.Where(Directory.Exists).ToList();
    }

    /// <summary>
    /// gets the appropriate executable name for the current platform
    /// </summary>
    public static string GetExecutableName(string baseName)
    {
        return CurrentPlatform switch
        {
            Platform.Windows => $"{baseName}.exe",
            Platform.macOS => baseName,
            Platform.Linux => baseName,
            _ => baseName
        };
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "access")]
    private static extern int access(string pathname, int mode);

    private static bool IsPosixExecutable(string filePath)
    {
        // X_OK check for execute permission
        const int X_OK = 1;
        return access(filePath, X_OK) == 0;
    }

    /// <summary>
    /// checks if a path points to a valid executable on the current platform
    /// </summary>
    public static bool IsValidExecutable(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        switch (CurrentPlatform)
        {
            case Platform.Windows:
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                return extension == ".exe" || extension == ".dll";
            case Platform.macOS:
            case Platform.Linux:
                return IsPosixExecutable(filePath);
            default:
                return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static List<string> GetWindowsSteamPaths()
    {
        var paths = new List<string>();

        try
        {
            // try registry first
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            var registryPath = key?.GetValue("InstallPath")?.ToString();
            if (!string.IsNullOrEmpty(registryPath) && Directory.Exists(registryPath))
            {
                paths.Add(registryPath);
            }

            // fallback to common paths
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam")
            };

            paths.AddRange(commonPaths);
        }
        catch
        {
            // ignore registry access errors
        }

        return paths;
    }

    private static List<string> GetMacOSSteamPaths()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new List<string>
        {
            Path.Combine(homeDir, "Library", "Application Support", "Steam"),
            Path.Combine(homeDir, "Applications", "Steam.app", "Contents", "MacOS"),
            "/Applications/Steam.app/Contents/MacOS"
        };
    }

    private static List<string> GetLinuxSteamPaths()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new List<string>
        {
            Path.Combine(homeDir, ".steam", "steam"),
            Path.Combine(homeDir, ".local", "share", "Steam"),
            Path.Combine(homeDir, "Steam"),
            "/usr/lib/steam",
            "/usr/lib64/steam",
            "/usr/local/lib/steam"
        };
    }

    private static List<string> GetWindowsGogPaths()
    {
        var paths = new List<string>();

        try
        {
            // try multiple registry locations for gog galaxy
            var registryPaths = new[]
            {
                @"SOFTWARE\GOG.com\Galaxy",
                @"SOFTWARE\WOW6432Node\GOG.com\Galaxy",
                @"SOFTWARE\GOG.com\Galaxy\Settings",
                @"SOFTWARE\WOW6432Node\GOG.com\Galaxy\Settings"
            };

            foreach (var registryPath in registryPaths)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryPath);
                var installPath = key?.GetValue("InstallPath")?.ToString();
                if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                    paths.Add(installPath);
            }
        }
        catch
        {
            // ignore registry access errors
        }

        // fallback to common installation paths
        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GOG.com", "Galaxy")
        };

        paths.AddRange(commonPaths);
        return paths;
    }

    private static List<string> GetMacOSGogPaths()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new List<string>
        {
            Path.Combine(homeDir, "Library", "Application Support", "GOG.com", "Galaxy"),
            Path.Combine(homeDir, "Applications", "GOG Galaxy.app", "Contents", "MacOS"),
            "/Applications/GOG Galaxy.app/Contents/MacOS"
        };
    }

    private static List<string> GetLinuxGogPaths()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new List<string>
        {
            Path.Combine(homeDir, ".local", "share", "GOG.com", "Galaxy"),
            Path.Combine(homeDir, ".gog", "Galaxy"),
            Path.Combine(homeDir, "Games", "GOG Galaxy"),
            "/usr/lib/gog-galaxy",
            "/usr/local/lib/gog-galaxy"
        };
    }

    private static List<string> GetWindowsEpicPaths()
    {
        var paths = new List<string>();

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Epic Games\EpicGamesLauncher");
            var registryPath = key?.GetValue("AppDataPath")?.ToString();
            if (!string.IsNullOrEmpty(registryPath) && Directory.Exists(registryPath))
            {
                paths.Add(registryPath);
            }
        }
        catch
        {
            // ignore registry access errors
        }

        // fallback to common paths
        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Epic", "EpicGamesLauncher")
        };

        paths.AddRange(commonPaths);
        return paths;
    }

    private static List<string> GetMacOSEpicPaths()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new List<string>
        {
            Path.Combine(homeDir, "Library", "Application Support", "Epic", "EpicGamesLauncher"),
            Path.Combine(homeDir, "Applications", "Epic Games Launcher.app", "Contents", "MacOS"),
            "/Applications/Epic Games Launcher.app/Contents/MacOS"
        };
    }

    private static List<string> GetLinuxEpicPaths()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new List<string>
        {
            Path.Combine(homeDir, ".local", "share", "Epic", "EpicGamesLauncher"),
            Path.Combine(homeDir, "Epic Games", "EpicGamesLauncher"),
            "/usr/lib/epic-games-launcher",
            "/usr/local/lib/epic-games-launcher"
        };
    }

    private static List<string> GetWindowsXboxPaths()
    {
        return new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WindowsApps"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "XboxGames"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "XboxLive"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Xbox Games")
        };
    }
}

/// <summary>
/// represents supported platforms
/// </summary>
public enum Platform
{
    Unknown,
    Windows,
    macOS,
    Linux
}

using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Microsoft.Win32;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace Lacesong.Core.Services;

/// <summary>
/// service for detecting game installations across different platforms and stores
/// </summary>
public class GameDetector : IGameDetector
{
    private readonly List<GameInstallation> _supportedGames;

    public GameDetector()
    {
        _supportedGames = LoadSupportedGames();
    }

    public async Task<GameInstallation?> DetectGameInstall(string? pathHint = null)
    {
        // if path hint provided, try to detect from that specific path
        if (!string.IsNullOrEmpty(pathHint))
        {
            var manualInstall = await DetectFromPath(pathHint);
            if (manualInstall != null)
                return manualInstall;
        }

        // try automatic detection from various sources
        var detectedGames = new List<GameInstallation>();

        // detect from steam
        var steamGames = await DetectFromSteam();
        detectedGames.AddRange(steamGames);

        // detect from epic games
        var epicGames = await DetectFromEpic();
        detectedGames.AddRange(epicGames);

        // detect from gog
        var gogGames = await DetectFromGog();
        detectedGames.AddRange(gogGames);

        // detect from xbox game pass
        var xboxGames = await DetectFromXbox();
        detectedGames.AddRange(xboxGames);

        // detect from common installation paths
        var commonPathGames = await DetectFromCommonPaths();
        detectedGames.AddRange(commonPathGames);

        // return the first valid installation found
        return detectedGames.FirstOrDefault(g => ValidateGameInstall(g));
    }

    public bool ValidateGameInstall(GameInstallation gameInstall)
    {
        if (string.IsNullOrEmpty(gameInstall.InstallPath) || string.IsNullOrEmpty(gameInstall.Executable))
            return false;

        // create the mods directory if not present so downstream services have it
        ModManager.EnsureModsDirectory(gameInstall);

        var executablePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
        return File.Exists(executablePath);
    }

    public async Task<List<GameInstallation>> GetSupportedGames()
    {
        return await Task.FromResult(_supportedGames);
    }

    private async Task<GameInstallation?> DetectFromPath(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return null;

            // look for game executables in the directory
            foreach (var game in _supportedGames)
            {
                var executableName = GetPlatformExecutableName(game.Executable);
                var executablePath = Path.Combine(path, executableName);

                // on macos, also check for .app bundles
                if (PlatformDetector.IsMacOS && !File.Exists(executablePath))
                {
                    var appBundlePath = Path.Combine(path, $"{Path.GetFileNameWithoutExtension(executableName)}.app");
                    if (Directory.Exists(appBundlePath))
                    {
                        executablePath = Path.Combine(appBundlePath, "Contents", "MacOS", Path.GetFileNameWithoutExtension(executableName));
                    }
                }

                if (PlatformDetector.IsValidExecutable(executablePath))
                {
                    return new GameInstallation
                    {
                        Name = game.Name,
                        Id = game.Id,
                        InstallPath = path,
                        Executable = executableName,
                        SteamAppId = game.SteamAppId,
                        EpicAppId = game.EpicAppId,
                        GogAppId = game.GogAppId,
                        XboxAppId = game.XboxAppId,
                        BepInExVersion = game.BepInExVersion,
                        ModDirectory = game.ModDirectory,
                        IsValid = true,
                        DetectedBy = "Manual Path"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            // log error but don't throw
            Console.WriteLine($"Error detecting from path {path}: {ex.Message}");
        }

        return null;
    }

    private async Task<List<GameInstallation>> DetectFromSteam()
    {
        var detectedGames = new List<GameInstallation>();

        try
        {
            // get steam installation paths for current platform
            var steamPaths = PlatformDetector.GetSteamPaths();
            if (steamPaths.Count == 0)
                return detectedGames;

            foreach (var steamPath in steamPaths)
            {
                var libraryPaths = new List<string> { steamPath };

                // on windows, parse library folders from vdf file
                if (PlatformDetector.IsWindows)
                {
                    var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(libraryFoldersPath))
                    {
                        libraryPaths.AddRange(ParseSteamLibraryFolders(libraryFoldersPath));
                    }
                }
                // on macos and linux, check common library locations
                else
                {
                    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (PlatformDetector.IsMacOS)
                    {
                        libraryPaths.AddRange(GetMacOSSteamLibraryPaths(homeDir));
                    }
                    else if (PlatformDetector.IsLinux)
                    {
                        libraryPaths.AddRange(GetLinuxSteamLibraryPaths(homeDir));
                    }
                }

                foreach (var libraryPath in libraryPaths.Distinct())
                {
                    var steamAppsPath = Path.Combine(libraryPath, "steamapps", "common");
                    if (!Directory.Exists(steamAppsPath))
                        continue;

                    foreach (var game in _supportedGames)
                    {
                        if (string.IsNullOrEmpty(game.SteamAppId))
                            continue;

                        // try different possible folder names for the game
                        var possibleFolderNames = new[]
                        {
                            game.Name, // "Hollow Knight: Silksong"
                            game.Name.Replace(":", ""), // "Hollow Knight Silksong"
                            "Hollow Knight Silksong" // explicit fallback
                        };

                        foreach (var folderName in possibleFolderNames)
                        {
                            var gamePath = Path.Combine(steamAppsPath, folderName);
                            var executableName = GetPlatformExecutableName(game.Executable);
                            var executablePath = Path.Combine(gamePath, executableName);

                            // on macos, also check for .app bundles
                            if (PlatformDetector.IsMacOS && !File.Exists(executablePath))
                            {
                                var appBundlePath = Path.Combine(gamePath, $"{Path.GetFileNameWithoutExtension(executableName)}.app");
                                if (Directory.Exists(appBundlePath))
                                {
                                    executablePath = Path.Combine(appBundlePath, "Contents", "MacOS", Path.GetFileNameWithoutExtension(executableName));
                                }
                            }

                            if (PlatformDetector.IsValidExecutable(executablePath))
                            {
                                detectedGames.Add(new GameInstallation
                                {
                                    Name = game.Name,
                                    Id = game.Id,
                                    InstallPath = gamePath,
                                    Executable = executableName,
                                    SteamAppId = game.SteamAppId,
                                    EpicAppId = game.EpicAppId,
                                    GogAppId = game.GogAppId,
                                    XboxAppId = game.XboxAppId,
                                    BepInExVersion = game.BepInExVersion,
                                    ModDirectory = game.ModDirectory,
                                    IsValid = true,
                                    DetectedBy = "Steam"
                                });
                                break; // found the game, no need to check other folder names
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting Steam games: {ex.Message}");
        }

        return detectedGames;
    }

    private async Task<List<GameInstallation>> DetectFromEpic()
    {
        var detectedGames = new List<GameInstallation>();

        try
        {
            // get epic games launcher paths for current platform
            var epicPaths = PlatformDetector.GetEpicPaths();
            if (epicPaths.Count == 0)
                return detectedGames;

            foreach (var epicPath in epicPaths)
            {
                // look for game manifests
                var manifestsPath = Path.Combine(epicPath, "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (!Directory.Exists(manifestsPath))
                    continue;

                var manifestFiles = Directory.GetFiles(manifestsPath, "*.item");
                foreach (var manifestFile in manifestFiles)
                {
                    var manifest = await ParseEpicManifest(manifestFile);
                    if (manifest == null)
                        continue;

                    foreach (var game in _supportedGames)
                    {
                        if (string.IsNullOrEmpty(game.EpicAppId) || manifest.AppId != game.EpicAppId)
                            continue;

                        var executableName = GetPlatformExecutableName(game.Executable);
                        var executablePath = Path.Combine(manifest.InstallLocation, executableName);

                        // on macos, also check for .app bundles
                        if (PlatformDetector.IsMacOS && !File.Exists(executablePath))
                        {
                            var appBundlePath = Path.Combine(manifest.InstallLocation, $"{Path.GetFileNameWithoutExtension(executableName)}.app");
                            if (Directory.Exists(appBundlePath))
                            {
                                executablePath = Path.Combine(appBundlePath, "Contents", "MacOS", Path.GetFileNameWithoutExtension(executableName));
                            }
                        }

                        if (PlatformDetector.IsValidExecutable(executablePath))
                        {
                            detectedGames.Add(new GameInstallation
                            {
                                Name = game.Name,
                                Id = game.Id,
                                InstallPath = manifest.InstallLocation,
                                Executable = executableName,
                                SteamAppId = game.SteamAppId,
                                EpicAppId = game.EpicAppId,
                                GogAppId = game.GogAppId,
                                XboxAppId = game.XboxAppId,
                                BepInExVersion = game.BepInExVersion,
                                ModDirectory = game.ModDirectory,
                                IsValid = true,
                                DetectedBy = "Epic Games"
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting Epic games: {ex.Message}");
        }

        return detectedGames;
    }

    private async Task<List<GameInstallation>> DetectFromGog()
    {
        var detectedGames = new List<GameInstallation>();

        try
        {
            // get gog galaxy installation paths for current platform
            var gogPaths = PlatformDetector.GetGogPaths();
            if (gogPaths.Count == 0)
                return detectedGames;

            // build common gog installation paths for current platform
            var commonPaths = new List<string>();
            foreach (var gogPath in gogPaths)
            {
                commonPaths.AddRange(new[]
                {
                    Path.Combine(gogPath, "Games"),
                    Path.Combine(gogPath, "Galaxy", "Games"),
                    Path.Combine(gogPath, "Applications"),
                    Path.Combine(gogPath, "Galaxy", "Applications")
                });
            }

            // add platform-specific common paths
            if (PlatformDetector.IsWindows)
            {
                commonPaths.AddRange(new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "Games"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GOG.com", "Galaxy", "Applications"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GOG.com", "Galaxy", "Games")
                });
            }
            else if (PlatformDetector.IsMacOS)
            {
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                commonPaths.AddRange(new[]
                {
                    Path.Combine(homeDir, "Applications", "GOG Galaxy", "Games"),
                    Path.Combine(homeDir, "Games", "GOG"),
                    Path.Combine(homeDir, "Library", "Application Support", "GOG.com", "Galaxy", "Games")
                });
            }
            else if (PlatformDetector.IsLinux)
            {
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                commonPaths.AddRange(new[]
                {
                    Path.Combine(homeDir, "Games", "GOG"),
                    Path.Combine(homeDir, ".local", "share", "GOG.com", "Galaxy", "Games"),
                    Path.Combine(homeDir, ".gog", "Games")
                });
            }

            foreach (var commonPath in commonPaths.Distinct())
            {
                if (!Directory.Exists(commonPath))
                    continue;

                foreach (var game in _supportedGames)
                {
                    // try different possible folder names for the game
                    var possibleFolderNames = new[]
                    {
                        game.Name, // "Hollow Knight: Silksong"
                        game.Name.Replace(":", ""), // "Hollow Knight Silksong"
                        "Hollow Knight Silksong" // explicit fallback
                    };

                    foreach (var folderName in possibleFolderNames)
                    {
                        var gamePath = Path.Combine(commonPath, folderName);
                        var executableName = GetPlatformExecutableName(game.Executable);
                        var executablePath = Path.Combine(gamePath, executableName);

                        // on macos, also check for .app bundles
                        if (PlatformDetector.IsMacOS && !File.Exists(executablePath))
                        {
                            var appBundlePath = Path.Combine(gamePath, $"{Path.GetFileNameWithoutExtension(executableName)}.app");
                            if (Directory.Exists(appBundlePath))
                            {
                                executablePath = Path.Combine(appBundlePath, "Contents", "MacOS", Path.GetFileNameWithoutExtension(executableName));
                            }
                        }

                        if (PlatformDetector.IsValidExecutable(executablePath))
                        {
                            detectedGames.Add(new GameInstallation
                            {
                                Name = game.Name,
                                Id = game.Id,
                                InstallPath = gamePath,
                                Executable = executableName,
                                SteamAppId = game.SteamAppId,
                                EpicAppId = game.EpicAppId,
                                GogAppId = game.GogAppId,
                                XboxAppId = game.XboxAppId,
                                BepInExVersion = game.BepInExVersion,
                                ModDirectory = game.ModDirectory,
                                IsValid = true,
                                DetectedBy = "GOG"
                            });
                            break; // found the game, no need to check other folder names
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting GOG games: {ex.Message}");
        }

        return detectedGames;
    }

    private async Task<List<GameInstallation>> DetectFromXbox()
    {
        var detectedGames = new List<GameInstallation>();

        try
        {
            // xbox game pass is only available on windows
            if (!PlatformDetector.IsWindows)
                return detectedGames;

            // xbox game pass installation paths
            var xboxPaths = PlatformDetector.GetXboxPaths();
            if (xboxPaths.Count == 0)
                return detectedGames;

            foreach (var xboxPath in xboxPaths)
            {
                if (!Directory.Exists(xboxPath))
                    continue;

                foreach (var game in _supportedGames)
                {
                    if (string.IsNullOrEmpty(game.XboxAppId))
                        continue;

                    // try different possible folder names for the game
                    var possibleFolderNames = new[]
                    {
                        game.Name, // "Hollow Knight: Silksong"
                        game.Name.Replace(":", ""), // "Hollow Knight Silksong"
                        "Hollow Knight Silksong", // explicit fallback
                        game.XboxAppId // use xbox app id as folder name
                    };

                    foreach (var folderName in possibleFolderNames)
                    {
                        var gamePath = Path.Combine(xboxPath, folderName);
                        var executableName = GetPlatformExecutableName(game.Executable);
                        var executablePath = Path.Combine(gamePath, executableName);

                        if (PlatformDetector.IsValidExecutable(executablePath))
                        {
                            detectedGames.Add(new GameInstallation
                            {
                                Name = game.Name,
                                Id = game.Id,
                                InstallPath = gamePath,
                                Executable = executableName,
                                SteamAppId = game.SteamAppId,
                                EpicAppId = game.EpicAppId,
                                GogAppId = game.GogAppId,
                                XboxAppId = game.XboxAppId,
                                BepInExVersion = game.BepInExVersion,
                                ModDirectory = game.ModDirectory,
                                IsValid = true,
                                DetectedBy = "Xbox Game Pass"
                            });
                            break; // found the game, no need to check other folder names
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting Xbox Game Pass games: {ex.Message}");
        }

        return detectedGames;
    }

    private async Task<List<GameInstallation>> DetectFromCommonPaths()
    {
        var detectedGames = new List<GameInstallation>();

        try
        {
            // get common game installation paths for current platform
            var commonPaths = GetPlatformCommonPaths();

            foreach (var commonPath in commonPaths)
            {
                if (!Directory.Exists(commonPath))
                    continue;

                foreach (var game in _supportedGames)
                {
                    // try different possible folder names for the game
                    var possibleFolderNames = new[]
                    {
                        game.Name, // "Hollow Knight: Silksong"
                        game.Name.Replace(":", ""), // "Hollow Knight Silksong"
                        "Hollow Knight Silksong" // explicit fallback
                    };

                    foreach (var folderName in possibleFolderNames)
                    {
                        var gamePath = Path.Combine(commonPath, folderName);
                        var executableName = GetPlatformExecutableName(game.Executable);
                        var executablePath = Path.Combine(gamePath, executableName);

                        // on macos, also check for .app bundles
                        if (PlatformDetector.IsMacOS && !File.Exists(executablePath))
                        {
                            var appBundlePath = Path.Combine(gamePath, $"{Path.GetFileNameWithoutExtension(executableName)}.app");
                            if (Directory.Exists(appBundlePath))
                            {
                                executablePath = Path.Combine(appBundlePath, "Contents", "MacOS", Path.GetFileNameWithoutExtension(executableName));
                            }
                        }

                        if (PlatformDetector.IsValidExecutable(executablePath))
                        {
                            detectedGames.Add(new GameInstallation
                            {
                                Name = game.Name,
                                Id = game.Id,
                                InstallPath = gamePath,
                                Executable = executableName,
                                SteamAppId = game.SteamAppId,
                                EpicAppId = game.EpicAppId,
                                GogAppId = game.GogAppId,
                                XboxAppId = game.XboxAppId,
                                BepInExVersion = game.BepInExVersion,
                                ModDirectory = game.ModDirectory,
                                IsValid = true,
                                DetectedBy = "Common Path"
                            });
                            break; // found the game, no need to check other folder names
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting from common paths: {ex.Message}");
        }

        return detectedGames;
    }

    private List<GameInstallation> LoadSupportedGames()
    {
        // load from manifest.json or return default configuration
        try
        {
            var manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "manifest.json");
            if (File.Exists(manifestPath))
            {
                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<JsonElement>(manifestJson);
                
                if (manifest.TryGetProperty("supportedGames", out var supportedGamesElement))
                {
                    var games = JsonSerializer.Deserialize<List<GameInstallation>>(supportedGamesElement.GetRawText());
                    return games ?? GetDefaultSupportedGames();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading supported games from manifest: {ex.Message}");
        }

        return GetDefaultSupportedGames();
    }

    private List<GameInstallation> GetDefaultSupportedGames()
    {
        var executableName = PlatformDetector.IsWindows ? "Hollow Knight Silksong.exe" : "Hollow Knight Silksong";
        var modDirectory = PlatformDetector.IsWindows 
            ? "Hollow Knight Silksong_Data\\Managed\\Mods" 
            : "Hollow Knight Silksong_Data/Managed/Mods";

        return new List<GameInstallation>
        {
            new GameInstallation
            {
                Name = "Hollow Knight: Silksong",
                Id = "hollow-knight-silksong",
                Executable = executableName,
                SteamAppId = "1030300",
                EpicAppId = "hollow-knight-silksong",
                GogAppId = "hollow_knight_silksong",
                XboxAppId = "9NQZQZQZQZQZ",
                BepInExVersion = "5.4.22",
                ModDirectory = modDirectory
            }
        };
    }


    private List<string> ParseSteamLibraryFolders(string libraryFoldersPath)
    {
        var libraryPaths = new List<string>();
        
        try
        {
            var content = File.ReadAllText(libraryFoldersPath);
            // simple parsing of steam library folders vdf file
            // this is a simplified implementation - in production you'd want a proper vdf parser
            var lines = content.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("\"path\""))
                {
                    var pathStart = line.IndexOf('"', line.IndexOf('"') + 1) + 1;
                    var pathEnd = line.LastIndexOf('"');
                    if (pathStart > 0 && pathEnd > pathStart)
                    {
                        var path = line.Substring(pathStart, pathEnd - pathStart);
                        if (Directory.Exists(path))
                            libraryPaths.Add(path);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing Steam library folders: {ex.Message}");
        }

        return libraryPaths;
    }

    private async Task<EpicManifest?> ParseEpicManifest(string manifestPath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<EpicManifest>(content);
            return manifest;
        }
        catch
        {
            return null;
        }
    }

    private class EpicManifest
    {
        public string AppId { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
    }

    /// <summary>
    /// gets the appropriate executable name for the current platform
    /// </summary>
    private string GetPlatformExecutableName(string baseExecutable)
    {
        return PlatformDetector.GetExecutableName(baseExecutable);
    }

    /// <summary>
    /// gets common installation paths for the current platform
    /// </summary>
    private List<string> GetPlatformCommonPaths()
    {
        var paths = new List<string>();

        if (PlatformDetector.IsWindows)
        {
            paths.AddRange(new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WindowsApps")
            });
        }
        else if (PlatformDetector.IsMacOS)
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            paths.AddRange(new[]
            {
                Path.Combine(homeDir, "Applications"),
                Path.Combine(homeDir, "Games"),
                Path.Combine(homeDir, "Library", "Application Support", "Steam", "steamapps", "common"),
                Path.Combine(homeDir, "Library", "Application Support", "Epic", "EpicGamesLauncher"),
                Path.Combine(homeDir, "Library", "Application Support", "GOG.com", "Galaxy", "Games")
            });
        }
        else if (PlatformDetector.IsLinux)
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            paths.AddRange(new[]
            {
                Path.Combine(homeDir, "Games"),
                Path.Combine(homeDir, ".local", "share", "Steam", "steamapps", "common"),
                Path.Combine(homeDir, ".steam", "steam", "steamapps", "common"),
                Path.Combine(homeDir, ".local", "share", "Epic", "EpicGamesLauncher"),
                Path.Combine(homeDir, ".local", "share", "GOG.com", "Galaxy", "Games"),
                Path.Combine(homeDir, ".gog", "Games"),
                "/usr/local/games",
                "/opt/games"
            });
        }

        return paths;
    }

    /// <summary>
    /// gets macos steam library paths
    /// </summary>
    private List<string> GetMacOSSteamLibraryPaths(string homeDir)
    {
        var paths = new List<string>
        {
            Path.Combine(homeDir, "Library", "Application Support", "Steam")
        };

        // check for additional steam library locations on macos
        var additionalPaths = new[]
        {
            Path.Combine(homeDir, "Games", "Steam"),
            Path.Combine(homeDir, "Documents", "Steam"),
            "/Applications/Steam.app/Contents/MacOS"
        };

        paths.AddRange(additionalPaths.Where(Directory.Exists));
        return paths;
    }

    /// <summary>
    /// gets linux steam library paths
    /// </summary>
    private List<string> GetLinuxSteamLibraryPaths(string homeDir)
    {
        var paths = new List<string>
        {
            Path.Combine(homeDir, ".steam", "steam"),
            Path.Combine(homeDir, ".local", "share", "Steam")
        };

        // check for additional steam library locations on linux
        var additionalPaths = new[]
        {
            Path.Combine(homeDir, "Games", "Steam"),
            Path.Combine(homeDir, "Steam"),
            "/usr/lib/steam",
            "/usr/lib64/steam",
            "/usr/local/lib/steam"
        };

        paths.AddRange(additionalPaths.Where(Directory.Exists));
        return paths;
    }
}

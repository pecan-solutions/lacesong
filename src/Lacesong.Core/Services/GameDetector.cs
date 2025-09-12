using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Microsoft.Win32;
using System.Text.Json;

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
                var executablePath = Path.Combine(path, game.Executable);
                if (File.Exists(executablePath))
                {
                    return new GameInstallation
                    {
                        Name = game.Name,
                        Id = game.Id,
                        InstallPath = path,
                        Executable = game.Executable,
                        SteamAppId = game.SteamAppId,
                        EpicAppId = game.EpicAppId,
                        GogAppId = game.GogAppId,
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
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return detectedGames;

            // steam installation path
            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath))
                return detectedGames;

            var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersPath))
                return detectedGames;

            // parse library folders and look for games
            var libraryPaths = ParseSteamLibraryFolders(libraryFoldersPath);
            
            foreach (var libraryPath in libraryPaths)
            {
                var steamAppsPath = Path.Combine(libraryPath, "steamapps", "common");
                if (!Directory.Exists(steamAppsPath))
                    continue;

                foreach (var game in _supportedGames)
                {
                    if (string.IsNullOrEmpty(game.SteamAppId))
                        continue;

                    var gamePath = Path.Combine(steamAppsPath, game.Name);
                    var executablePath = Path.Combine(gamePath, game.Executable);

                    if (File.Exists(executablePath))
                    {
                        detectedGames.Add(new GameInstallation
                        {
                            Name = game.Name,
                            Id = game.Id,
                            InstallPath = gamePath,
                            Executable = game.Executable,
                            SteamAppId = game.SteamAppId,
                            EpicAppId = game.EpicAppId,
                            GogAppId = game.GogAppId,
                            BepInExVersion = game.BepInExVersion,
                            ModDirectory = game.ModDirectory,
                            IsValid = true,
                            DetectedBy = "Steam"
                        });
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
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return detectedGames;

            // epic games launcher installation path
            var epicPath = GetEpicInstallPath();
            if (string.IsNullOrEmpty(epicPath))
                return detectedGames;

            var manifestsPath = Path.Combine(epicPath, "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(manifestsPath))
                return detectedGames;

            // look for game manifests
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

                    var executablePath = Path.Combine(manifest.InstallLocation, game.Executable);
                    if (File.Exists(executablePath))
                    {
                        detectedGames.Add(new GameInstallation
                        {
                            Name = game.Name,
                            Id = game.Id,
                            InstallPath = manifest.InstallLocation,
                            Executable = game.Executable,
                            SteamAppId = game.SteamAppId,
                            EpicAppId = game.EpicAppId,
                            GogAppId = game.GogAppId,
                            BepInExVersion = game.BepInExVersion,
                            ModDirectory = game.ModDirectory,
                            IsValid = true,
                            DetectedBy = "Epic Games"
                        });
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
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return detectedGames;

            // gog galaxy installation path
            var gogPath = GetGogInstallPath();
            if (string.IsNullOrEmpty(gogPath))
                return detectedGames;

            // look for games in common gog installation paths
            var commonPaths = new[]
            {
                Path.Combine(gogPath, "Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games")
            };

            foreach (var commonPath in commonPaths)
            {
                if (!Directory.Exists(commonPath))
                    continue;

                foreach (var game in _supportedGames)
                {
                    var gamePath = Path.Combine(commonPath, game.Name);
                    var executablePath = Path.Combine(gamePath, game.Executable);

                    if (File.Exists(executablePath))
                    {
                        detectedGames.Add(new GameInstallation
                        {
                            Name = game.Name,
                            Id = game.Id,
                            InstallPath = gamePath,
                            Executable = game.Executable,
                            SteamAppId = game.SteamAppId,
                            EpicAppId = game.EpicAppId,
                            GogAppId = game.GogAppId,
                            BepInExVersion = game.BepInExVersion,
                            ModDirectory = game.ModDirectory,
                            IsValid = true,
                            DetectedBy = "GOG"
                        });
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

    private async Task<List<GameInstallation>> DetectFromCommonPaths()
    {
        var detectedGames = new List<GameInstallation>();

        try
        {
            // common game installation paths
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games")
            };

            foreach (var commonPath in commonPaths)
            {
                if (!Directory.Exists(commonPath))
                    continue;

                foreach (var game in _supportedGames)
                {
                    var gamePath = Path.Combine(commonPath, game.Name);
                    var executablePath = Path.Combine(gamePath, game.Executable);

                    if (File.Exists(executablePath))
                    {
                        detectedGames.Add(new GameInstallation
                        {
                            Name = game.Name,
                            Id = game.Id,
                            InstallPath = gamePath,
                            Executable = game.Executable,
                            SteamAppId = game.SteamAppId,
                            EpicAppId = game.EpicAppId,
                            GogAppId = game.GogAppId,
                            BepInExVersion = game.BepInExVersion,
                            ModDirectory = game.ModDirectory,
                            IsValid = true,
                            DetectedBy = "Common Path"
                        });
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
        return new List<GameInstallation>
        {
            new GameInstallation
            {
                Name = "Hollow Knight: Silksong",
                Id = "hollow-knight-silksong",
                Executable = "Hollow Knight Silksong.exe",
                SteamAppId = "1030300",
                EpicAppId = "hollow-knight-silksong",
                GogAppId = "hollow_knight_silksong",
                BepInExVersion = "5.4.22",
                ModDirectory = "BepInEx/plugins"
            }
        };
    }

    private string? GetSteamInstallPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            return key?.GetValue("InstallPath")?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private string? GetEpicInstallPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Epic Games\EpicGamesLauncher");
            return key?.GetValue("AppDataPath")?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private string? GetGogInstallPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\Galaxy");
            return key?.GetValue("InstallPath")?.ToString();
        }
        catch
        {
            return null;
        }
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
}

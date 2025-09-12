using System.CommandLine;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Core.Services;

namespace Lacesong.CLI;

/// <summary>
/// main entry point for the lacesong cli tool
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // create root command
        var rootCommand = new RootCommand("Lacesong Mod Manager CLI - A cross-platform mod management tool for Unity/Mono games");

        // create services
        var gameDetector = new GameDetector();
        var bepinexManager = new BepInExManager();
        var modManager = new ModManager();
        var backupManager = new BackupManager();

        // install-bepinex command
        var installBepInExCommand = new Command("install-bepinex", "Install BepInEx to a game installation");
        var pathOption = new Option<string?>("--path", "Path to game installation directory");
        var versionOption = new Option<string>("--version", () => "5.4.22", "BepInEx version to install");
        var forceOption = new Option<bool>("--force", "Force reinstall even if already installed");
        var backupOption = new Option<bool>("--backup", () => true, "Create backup before installation");
        var shortcutOption = new Option<bool>("--shortcut", "Create desktop shortcut");

        installBepInExCommand.AddOption(pathOption);
        installBepInExCommand.AddOption(versionOption);
        installBepInExCommand.AddOption(forceOption);
        installBepInExCommand.AddOption(backupOption);
        installBepInExCommand.AddOption(shortcutOption);

        installBepInExCommand.SetHandler(async (path, version, force, backup, shortcut) =>
        {
            await HandleInstallBepInEx(gameDetector, bepinexManager, path, version, force, backup, shortcut);
        }, pathOption, versionOption, forceOption, backupOption, shortcutOption);

        // install-mod command
        var installModCommand = new Command("install-mod", "Install a mod from zip file or URL");
        var modSourceArgument = new Argument<string>("source", "Path to mod zip file or download URL");
        var modPathOption = new Option<string?>("--path", "Path to game installation directory");

        installModCommand.AddArgument(modSourceArgument);
        installModCommand.AddOption(modPathOption);

        installModCommand.SetHandler(async (source, path) =>
        {
            await HandleInstallMod(gameDetector, modManager, source, path);
        }, modSourceArgument, modPathOption);

        // uninstall-mod command
        var uninstallModCommand = new Command("uninstall-mod", "Uninstall a mod by ID");
        var modIdArgument = new Argument<string>("mod-id", "ID of the mod to uninstall");
        var uninstallPathOption = new Option<string?>("--path", "Path to game installation directory");

        uninstallModCommand.AddArgument(modIdArgument);
        uninstallModCommand.AddOption(uninstallPathOption);

        uninstallModCommand.SetHandler(async (modId, path) =>
        {
            await HandleUninstallMod(gameDetector, modManager, modId, path);
        }, modIdArgument, uninstallPathOption);

        // enable-mod command
        var enableModCommand = new Command("enable-mod", "Enable a mod by ID");
        var enableModIdArgument = new Argument<string>("mod-id", "ID of the mod to enable");
        var enablePathOption = new Option<string?>("--path", "Path to game installation directory");

        enableModCommand.AddArgument(enableModIdArgument);
        enableModCommand.AddOption(enablePathOption);

        enableModCommand.SetHandler(async (modId, path) =>
        {
            await HandleEnableMod(gameDetector, modManager, modId, path);
        }, enableModIdArgument, enablePathOption);

        // disable-mod command
        var disableModCommand = new Command("disable-mod", "Disable a mod by ID");
        var disableModIdArgument = new Argument<string>("mod-id", "ID of the mod to disable");
        var disablePathOption = new Option<string?>("--path", "Path to game installation directory");

        disableModCommand.AddArgument(disableModIdArgument);
        disableModCommand.AddOption(disablePathOption);

        disableModCommand.SetHandler(async (modId, path) =>
        {
            await HandleDisableMod(gameDetector, modManager, modId, path);
        }, disableModIdArgument, disablePathOption);

        // list-mods command
        var listModsCommand = new Command("list-mods", "List all installed mods");
        var listPathOption = new Option<string?>("--path", "Path to game installation directory");

        listModsCommand.AddOption(listPathOption);

        listModsCommand.SetHandler(async (path) =>
        {
            await HandleListMods(gameDetector, modManager, path);
        }, listPathOption);

        // backup command
        var backupCommand = new Command("backup", "Create a backup of current mod configuration");
        var backupNameArgument = new Argument<string>("name", "Name for the backup");
        var backupPathOption = new Option<string?>("--path", "Path to game installation directory");

        backupCommand.AddArgument(backupNameArgument);
        backupCommand.AddOption(backupPathOption);

        backupCommand.SetHandler(async (name, path) =>
        {
            await HandleBackup(gameDetector, backupManager, name, path);
        }, backupNameArgument, backupPathOption);

        // restore command
        var restoreCommand = new Command("restore", "Restore a backup");
        var restoreFileArgument = new Argument<string>("backup-file", "Path to backup file");
        var restorePathOption = new Option<string?>("--path", "Path to game installation directory");

        restoreCommand.AddArgument(restoreFileArgument);
        restoreCommand.AddOption(restorePathOption);

        restoreCommand.SetHandler(async (backupFile, path) =>
        {
            await HandleRestore(gameDetector, backupManager, backupFile, path);
        }, restoreFileArgument, restorePathOption);

        // detect-game command
        var detectGameCommand = new Command("detect-game", "Detect game installation");
        var detectPathOption = new Option<string?>("--path", "Path hint for game detection");

        detectGameCommand.AddOption(detectPathOption);

        detectGameCommand.SetHandler(async (path) =>
        {
            await HandleDetectGame(gameDetector, path);
        }, detectPathOption);

        // add all commands to root command
        rootCommand.AddCommand(installBepInExCommand);
        rootCommand.AddCommand(installModCommand);
        rootCommand.AddCommand(uninstallModCommand);
        rootCommand.AddCommand(enableModCommand);
        rootCommand.AddCommand(disableModCommand);
        rootCommand.AddCommand(listModsCommand);
        rootCommand.AddCommand(backupCommand);
        rootCommand.AddCommand(restoreCommand);
        rootCommand.AddCommand(detectGameCommand);

        // execute the command
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task HandleInstallBepInEx(IGameDetector gameDetector, IBepInExManager bepinexManager, 
        string? path, string version, bool force, bool backup, bool shortcut)
    {
        try
        {
            Console.WriteLine("Detecting game installation...");
            var gameInstall = await gameDetector.DetectGameInstall(path);
            
            if (gameInstall == null)
            {
                Console.WriteLine("Error: Could not detect game installation. Please specify --path or ensure the game is installed.");
                return;
            }

            Console.WriteLine($"Detected game: {gameInstall.Name} at {gameInstall.InstallPath}");

            var options = new BepInExInstallOptions
            {
                Version = version,
                ForceReinstall = force,
                BackupExisting = backup,
                CreateDesktopShortcut = shortcut
            };

            Console.WriteLine("Installing BepInEx...");
            var result = await bepinexManager.InstallBepInEx(gameInstall, options);
            
            if (result.Success)
            {
                Console.WriteLine($"Success: {result.Message}");
            }
            else
            {
                Console.WriteLine($"Error: {result.Message}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"Details: {result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleInstallMod(IGameDetector gameDetector, IModManager modManager, 
        string source, string? path)
    {
        try
        {
            Console.WriteLine("Detecting game installation...");
            var gameInstall = await gameDetector.DetectGameInstall(path);
            
            if (gameInstall == null)
            {
                Console.WriteLine("Error: Could not detect game installation. Please specify --path or ensure the game is installed.");
                return;
            }

            Console.WriteLine($"Detected game: {gameInstall.Name} at {gameInstall.InstallPath}");

            Console.WriteLine($"Installing mod from: {source}");
            var result = await modManager.InstallModFromZip(source, gameInstall);
            
            if (result.Success)
            {
                Console.WriteLine($"Success: {result.Message}");
                if (result.Data is ModInfo modInfo)
                {
                    Console.WriteLine($"Installed mod: {modInfo.Name} v{modInfo.Version}");
                }
            }
            else
            {
                Console.WriteLine($"Error: {result.Message}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"Details: {result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleUninstallMod(IGameDetector gameDetector, IModManager modManager, 
        string modId, string? path)
    {
        try
        {
            Console.WriteLine("Detecting game installation...");
            var gameInstall = await gameDetector.DetectGameInstall(path);
            
            if (gameInstall == null)
            {
                Console.WriteLine("Error: Could not detect game installation. Please specify --path or ensure the game is installed.");
                return;
            }

            Console.WriteLine($"Uninstalling mod: {modId}");
            var result = await modManager.UninstallMod(modId, gameInstall);
            
            if (result.Success)
            {
                Console.WriteLine($"Success: {result.Message}");
            }
            else
            {
                Console.WriteLine($"Error: {result.Message}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"Details: {result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleEnableMod(IGameDetector gameDetector, IModManager modManager, 
        string modId, string? path)
    {
        try
        {
            Console.WriteLine("Detecting game installation...");
            var gameInstall = await gameDetector.DetectGameInstall(path);
            
            if (gameInstall == null)
            {
                Console.WriteLine("Error: Could not detect game installation. Please specify --path or ensure the game is installed.");
                return;
            }

            Console.WriteLine($"Enabling mod: {modId}");
            var result = await modManager.EnableMod(modId, gameInstall);
            
            if (result.Success)
            {
                Console.WriteLine($"Success: {result.Message}");
            }
            else
            {
                Console.WriteLine($"Error: {result.Message}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"Details: {result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleDisableMod(IGameDetector gameDetector, IModManager modManager, 
        string modId, string? path)
    {
        try
        {
            Console.WriteLine("Detecting game installation...");
            var gameInstall = await gameDetector.DetectGameInstall(path);
            
            if (gameInstall == null)
            {
                Console.WriteLine("Error: Could not detect game installation. Please specify --path or ensure the game is installed.");
                return;
            }

            Console.WriteLine($"Disabling mod: {modId}");
            var result = await modManager.DisableMod(modId, gameInstall);
            
            if (result.Success)
            {
                Console.WriteLine($"Success: {result.Message}");
            }
            else
            {
                Console.WriteLine($"Error: {result.Message}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"Details: {result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleListMods(IGameDetector gameDetector, IModManager modManager, string? path)
    {
        try
        {
            Console.WriteLine("Detecting game installation...");
            var gameInstall = await gameDetector.DetectGameInstall(path);
            
            if (gameInstall == null)
            {
                Console.WriteLine("Error: Could not detect game installation. Please specify --path or ensure the game is installed.");
                return;
            }

            Console.WriteLine($"Listing mods for: {gameInstall.Name}");
            var mods = await modManager.GetInstalledMods(gameInstall);
            
            if (mods.Count == 0)
            {
                Console.WriteLine("No mods installed.");
                return;
            }

            Console.WriteLine($"Found {mods.Count} installed mod(s):");
            Console.WriteLine();
            
            foreach (var mod in mods)
            {
                var status = mod.IsEnabled ? "Enabled" : "Disabled";
                Console.WriteLine($"  {mod.Name} v{mod.Version} ({status})");
                Console.WriteLine($"    ID: {mod.Id}");
                Console.WriteLine($"    Author: {mod.Author}");
                if (!string.IsNullOrEmpty(mod.Description))
                {
                    Console.WriteLine($"    Description: {mod.Description}");
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleBackup(IGameDetector gameDetector, IBackupManager backupManager, 
        string name, string? path)
    {
        try
        {
            Console.WriteLine("Detecting game installation...");
            var gameInstall = await gameDetector.DetectGameInstall(path);
            
            if (gameInstall == null)
            {
                Console.WriteLine("Error: Could not detect game installation. Please specify --path or ensure the game is installed.");
                return;
            }

            Console.WriteLine($"Creating backup: {name}");
            var result = await backupManager.CreateBackup(gameInstall, name);
            
            if (result.Success)
            {
                Console.WriteLine($"Success: {result.Message}");
                if (result.Data is string backupPath)
                {
                    Console.WriteLine($"Backup saved to: {backupPath}");
                }
            }
            else
            {
                Console.WriteLine($"Error: {result.Message}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"Details: {result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleRestore(IGameDetector gameDetector, IBackupManager backupManager, 
        string backupFile, string? path)
    {
        try
        {
            Console.WriteLine("Detecting game installation...");
            var gameInstall = await gameDetector.DetectGameInstall(path);
            
            if (gameInstall == null)
            {
                Console.WriteLine("Error: Could not detect game installation. Please specify --path or ensure the game is installed.");
                return;
            }

            Console.WriteLine($"Restoring backup: {backupFile}");
            var result = await backupManager.RestoreBackup(backupFile, gameInstall);
            
            if (result.Success)
            {
                Console.WriteLine($"Success: {result.Message}");
            }
            else
            {
                Console.WriteLine($"Error: {result.Message}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"Details: {result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleDetectGame(IGameDetector gameDetector, string? path)
    {
        try
        {
            Console.WriteLine("Detecting game installation...");
            var gameInstall = await gameDetector.DetectGameInstall(path);
            
            if (gameInstall == null)
            {
                Console.WriteLine("No game installation detected.");
                return;
            }

            Console.WriteLine("Game installation detected:");
            Console.WriteLine($"  Name: {gameInstall.Name}");
            Console.WriteLine($"  ID: {gameInstall.Id}");
            Console.WriteLine($"  Path: {gameInstall.InstallPath}");
            Console.WriteLine($"  Executable: {gameInstall.Executable}");
            Console.WriteLine($"  Detected by: {gameInstall.DetectedBy}");
            Console.WriteLine($"  Valid: {gameInstall.IsValid}");
            
            if (!string.IsNullOrEmpty(gameInstall.SteamAppId))
            {
                Console.WriteLine($"  Steam App ID: {gameInstall.SteamAppId}");
            }
            if (!string.IsNullOrEmpty(gameInstall.EpicAppId))
            {
                Console.WriteLine($"  Epic App ID: {gameInstall.EpicAppId}");
            }
            if (!string.IsNullOrEmpty(gameInstall.GogAppId))
            {
                Console.WriteLine($"  GOG App ID: {gameInstall.GogAppId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }
}

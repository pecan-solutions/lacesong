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
        var modIndexService = new ModIndexService();
        var verificationService = new VerificationService();
        var dependencyResolver = new DependencyResolver(modManager, bepinexManager);
        var installationStager = new InstallationStager(verificationService, dependencyResolver);
        var permissionsService = new PermissionsService();

        // install-bepinex command
        var installBepInExCommand = new Command("install-bepinex", "Install BepInEx to a game installation");
        var pathOption = new Option<string?>("--path", "Path to game installation directory");
        var versionOption = new Option<string>("--version", () => "5.4.22", "BepInEx version to install");
        var forceOption = new Option<bool>("--force", "Force reinstall even if already installed");
        var backupOption = new Option<bool>("--backup", () => true, "Create backup before installation");
        var shortcutOption = new Option<bool>("--shortcut", "Create desktop shortcut");
        var verifySignatureOption = new Option<bool>("--verify-signature", () => true, "Verify BepInEx signature");
        var verifyChecksumOption = new Option<bool>("--verify-checksum", () => true, "Verify BepInEx checksum");
        var requireElevationOption = new Option<bool>("--require-elevation", "Require administrator elevation");

        installBepInExCommand.AddOption(pathOption);
        installBepInExCommand.AddOption(versionOption);
        installBepInExCommand.AddOption(forceOption);
        installBepInExCommand.AddOption(backupOption);
        installBepInExCommand.AddOption(shortcutOption);
        installBepInExCommand.AddOption(verifySignatureOption);
        installBepInExCommand.AddOption(verifyChecksumOption);
        installBepInExCommand.AddOption(requireElevationOption);

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

        // search-mods command
        var searchModsCommand = new Command("search-mods", "Search for mods in the mod index");
        var queryOption = new Option<string?>("--query", "Search query");
        var categoryOption = new Option<string?>("--category", "Filter by category");
        var authorOption = new Option<string?>("--author", "Filter by author");
        var officialOption = new Option<bool?>("--official", "Filter by official status");
        var verifiedOption = new Option<bool?>("--verified", "Filter by verified status");
        var pageOption = new Option<int>("--page", () => 1, "Page number");
        var pageSizeOption = new Option<int>("--page-size", () => 20, "Number of results per page");

        searchModsCommand.AddOption(queryOption);
        searchModsCommand.AddOption(categoryOption);
        searchModsCommand.AddOption(authorOption);
        searchModsCommand.AddOption(officialOption);
        searchModsCommand.AddOption(verifiedOption);
        searchModsCommand.AddOption(pageOption);
        searchModsCommand.AddOption(pageSizeOption);

        searchModsCommand.SetHandler(async (query, category, author, official, verified, page, pageSize) =>
        {
            await HandleSearchMods(modIndexService, query, category, author, official, verified, page, pageSize);
        }, queryOption, categoryOption, authorOption, officialOption, verifiedOption, pageOption, pageSizeOption);

        // browse-mods command
        var browseModsCommand = new Command("browse-mods", "Browse available mods by category");
        var browseCategoryOption = new Option<string?>("--category", "Category to browse");

        browseModsCommand.AddOption(browseCategoryOption);

        browseModsCommand.SetHandler(async (category) =>
        {
            await HandleBrowseMods(modIndexService, category);
        }, browseCategoryOption);

        // add-repo command
        var addRepoCommand = new Command("add-repo", "Add a custom mod repository");
        var repoIdArgument = new Argument<string>("id", "Repository ID");
        var repoNameArgument = new Argument<string>("name", "Repository name");
        var repoUrlArgument = new Argument<string>("url", "Repository URL");
        var repoTypeOption = new Option<string>("--type", () => "Custom", "Repository type (GitHub, GitLab, Custom)");
        var repoDescriptionOption = new Option<string?>("--description", "Repository description");

        addRepoCommand.AddArgument(repoIdArgument);
        addRepoCommand.AddArgument(repoNameArgument);
        addRepoCommand.AddArgument(repoUrlArgument);
        addRepoCommand.AddOption(repoTypeOption);
        addRepoCommand.AddOption(repoDescriptionOption);

        addRepoCommand.SetHandler(async (id, name, url, type, description) =>
        {
            await HandleAddRepo(modIndexService, id, name, url, type, description);
        }, repoIdArgument, repoNameArgument, repoUrlArgument, repoTypeOption, repoDescriptionOption);

        // remove-repo command
        var removeRepoCommand = new Command("remove-repo", "Remove a custom mod repository");
        var removeRepoIdArgument = new Argument<string>("id", "Repository ID to remove");

        removeRepoCommand.AddArgument(removeRepoIdArgument);

        removeRepoCommand.SetHandler(async (id) =>
        {
            await HandleRemoveRepo(modIndexService, id);
        }, removeRepoIdArgument);

        // list-repos command
        var listReposCommand = new Command("list-repos", "List all configured repositories");

        listReposCommand.SetHandler(async () =>
        {
            await HandleListRepos(modIndexService);
        });

        // refresh-index command
        var refreshIndexCommand = new Command("refresh-index", "Refresh the mod index from all repositories");

        refreshIndexCommand.SetHandler(async () =>
        {
            await HandleRefreshIndex(modIndexService);
        });

        // install-from-index command
        var installFromIndexCommand = new Command("install-from-index", "Install a mod from the mod index");
        var indexModIdArgument = new Argument<string>("mod-id", "Mod ID from the index");
        var indexPathOption = new Option<string?>("--path", "Path to game installation directory");
        var indexVersionOption = new Option<string?>("--version", "Specific version to install");

        installFromIndexCommand.AddArgument(indexModIdArgument);
        installFromIndexCommand.AddOption(indexPathOption);
        installFromIndexCommand.AddOption(indexVersionOption);

        installFromIndexCommand.SetHandler(async (modId, path, version) =>
        {
            await HandleInstallFromIndex(gameDetector, modManager, modIndexService, modId, path, version);
        }, indexModIdArgument, indexPathOption, indexVersionOption);

        // verify-checksum command
        var verifyChecksumCommand = new Command("verify-checksum", "Verify file checksum");
        var checksumFileArgument = new Argument<string>("file", "File to verify");
        var checksumExpectedArgument = new Argument<string>("expected", "Expected checksum");
        var checksumAlgorithmOption = new Option<string>("--algorithm", () => "SHA256", "Checksum algorithm (SHA1, SHA256, SHA384, SHA512, MD5)");

        verifyChecksumCommand.AddArgument(checksumFileArgument);
        verifyChecksumCommand.AddArgument(checksumExpectedArgument);
        verifyChecksumCommand.AddOption(checksumAlgorithmOption);

        verifyChecksumCommand.SetHandler(async (file, expected, algorithm) =>
        {
            await HandleVerifyChecksum(verificationService, file, expected, algorithm);
        }, checksumFileArgument, checksumExpectedArgument, checksumAlgorithmOption);

        // check-permissions command
        var checkPermissionsCommand = new Command("check-permissions", "Check user permissions for game installation");
        var permissionsPathOption = new Option<string?>("--path", "Path to game installation directory");

        checkPermissionsCommand.AddOption(permissionsPathOption);

        checkPermissionsCommand.SetHandler(async (path) =>
        {
            await HandleCheckPermissions(gameDetector, permissionsService, path);
        }, permissionsPathOption);

        // create-restore-point command
        var createRestorePointCommand = new Command("create-restore-point", "Create a restore point");
        var restorePointNameArgument = new Argument<string>("name", "Name for the restore point");
        var restorePointDescriptionOption = new Option<string?>("--description", "Description for the restore point");
        var restorePointTagsOption = new Option<List<string>>("--tags", "Tags for the restore point");
        var restorePointPathOption = new Option<string?>("--path", "Path to game installation directory");

        createRestorePointCommand.AddArgument(restorePointNameArgument);
        createRestorePointCommand.AddOption(restorePointDescriptionOption);
        createRestorePointCommand.AddOption(restorePointTagsOption);
        createRestorePointCommand.AddOption(restorePointPathOption);

        createRestorePointCommand.SetHandler(async (name, description, tags, path) =>
        {
            await HandleCreateRestorePoint(gameDetector, backupManager, name, description, tags, path);
        }, restorePointNameArgument, restorePointDescriptionOption, restorePointTagsOption, restorePointPathOption);

        // list-restore-points command
        var listRestorePointsCommand = new Command("list-restore-points", "List all restore points");
        var listRestorePointsPathOption = new Option<string?>("--path", "Path to game installation directory");

        listRestorePointsCommand.AddOption(listRestorePointsPathOption);

        listRestorePointsCommand.SetHandler(async (path) =>
        {
            await HandleListRestorePoints(gameDetector, backupManager, path);
        }, listRestorePointsPathOption);

        // restore-from-point command
        var restoreFromPointCommand = new Command("restore-from-point", "Restore from a restore point");
        var restorePointFileArgument = new Argument<string>("restore-point-file", "Path to restore point file");
        var restoreFromPointPathOption = new Option<string?>("--path", "Path to game installation directory");

        restoreFromPointCommand.AddArgument(restorePointFileArgument);
        restoreFromPointCommand.AddOption(restoreFromPointPathOption);

        restoreFromPointCommand.SetHandler(async (restorePointFile, path) =>
        {
            await HandleRestoreFromPoint(gameDetector, backupManager, restorePointFile, path);
        }, restorePointFileArgument, restoreFromPointPathOption);

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
        rootCommand.AddCommand(searchModsCommand);
        rootCommand.AddCommand(browseModsCommand);
        rootCommand.AddCommand(addRepoCommand);
        rootCommand.AddCommand(removeRepoCommand);
        rootCommand.AddCommand(listReposCommand);
        rootCommand.AddCommand(refreshIndexCommand);
        rootCommand.AddCommand(installFromIndexCommand);
        rootCommand.AddCommand(verifyChecksumCommand);
        rootCommand.AddCommand(checkPermissionsCommand);
        rootCommand.AddCommand(createRestorePointCommand);
        rootCommand.AddCommand(listRestorePointsCommand);
        rootCommand.AddCommand(restoreFromPointCommand);

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

    private static async Task HandleSearchMods(IModIndexService modIndexService, string? query, string? category, 
        string? author, bool? official, bool? verified, int page, int pageSize)
    {
        try
        {
            Console.WriteLine("Searching mod index...");
            
            var criteria = new ModSearchCriteria
            {
                Query = query,
                Category = category,
                Author = author,
                IsOfficial = official,
                IsVerified = verified,
                Page = page,
                PageSize = pageSize
            };
            
            var results = await modIndexService.SearchMods(criteria);
            
            Console.WriteLine($"Found {results.TotalCount} mod(s) (Page {results.Page} of {results.TotalPages})");
            Console.WriteLine($"Search completed in {results.SearchTime.TotalMilliseconds:F0}ms");
            Console.WriteLine();
            
            if (results.Mods.Count == 0)
            {
                Console.WriteLine("No mods found matching your criteria.");
                return;
            }
            
            foreach (var mod in results.Mods)
            {
                var officialStatus = mod.IsOfficial ? " [Official]" : "";
                var verifiedStatus = mod.IsVerified ? " [Verified]" : "";
                
                Console.WriteLine($"  {mod.Name} v{mod.Versions.FirstOrDefault()?.Version ?? "Unknown"}{officialStatus}{verifiedStatus}");
                Console.WriteLine($"    ID: {mod.Id}");
                Console.WriteLine($"    Author: {mod.Author}");
                Console.WriteLine($"    Category: {mod.Category}");
                Console.WriteLine($"    Downloads: {mod.DownloadCount:N0}");
                if (mod.Rating > 0)
                {
                    Console.WriteLine($"    Rating: {mod.Rating:F1}/5.0 ({mod.RatingCount} reviews)");
                }
                if (!string.IsNullOrEmpty(mod.Description))
                {
                    var shortDesc = mod.Description.Length > 100 ? mod.Description[..100] + "..." : mod.Description;
                    Console.WriteLine($"    Description: {shortDesc}");
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleBrowseMods(IModIndexService modIndexService, string? category)
    {
        try
        {
            Console.WriteLine("Browsing mod index...");
            
            var categories = await modIndexService.GetCategories();
            
            if (string.IsNullOrEmpty(category))
            {
                Console.WriteLine("Available categories:");
                foreach (var cat in categories)
                {
                    Console.WriteLine($"  - {cat}");
                }
                return;
            }
            
            var criteria = new ModSearchCriteria
            {
                Category = category,
                PageSize = 50
            };
            
            var results = await modIndexService.SearchMods(criteria);
            
            Console.WriteLine($"Mods in '{category}' category:");
            Console.WriteLine($"Found {results.TotalCount} mod(s)");
            Console.WriteLine();
            
            foreach (var mod in results.Mods)
            {
                Console.WriteLine($"  {mod.Name} v{mod.Versions.FirstOrDefault()?.Version ?? "Unknown"}");
                Console.WriteLine($"    ID: {mod.Id} | Author: {mod.Author}");
                Console.WriteLine($"    Downloads: {mod.DownloadCount:N0}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleAddRepo(IModIndexService modIndexService, string id, string name, 
        string url, string type, string? description)
    {
        try
        {
            Console.WriteLine($"Adding repository: {name}");
            
            var repository = new ModRepository
            {
                Id = id,
                Name = name,
                Url = url,
                Type = type,
                Description = description,
                IsOfficial = false,
                IsEnabled = true
            };
            
            var result = await modIndexService.AddRepository(repository);
            
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

    private static async Task HandleRemoveRepo(IModIndexService modIndexService, string id)
    {
        try
        {
            Console.WriteLine($"Removing repository: {id}");
            
            var result = await modIndexService.RemoveRepository(id);
            
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

    private static async Task HandleListRepos(IModIndexService modIndexService)
    {
        try
        {
            Console.WriteLine("Listing configured repositories...");
            
            var repositories = await modIndexService.GetRepositories();
            
            if (repositories.Count == 0)
            {
                Console.WriteLine("No repositories configured.");
                return;
            }
            
            Console.WriteLine($"Found {repositories.Count} repository(ies):");
            Console.WriteLine();
            
            foreach (var repo in repositories)
            {
                var status = repo.IsEnabled ? "Enabled" : "Disabled";
                var officialStatus = repo.IsOfficial ? " [Official]" : "";
                
                Console.WriteLine($"  {repo.Name}{officialStatus} ({status})");
                Console.WriteLine($"    ID: {repo.Id}");
                Console.WriteLine($"    URL: {repo.Url}");
                Console.WriteLine($"    Type: {repo.Type}");
                if (!string.IsNullOrEmpty(repo.Description))
                {
                    Console.WriteLine($"    Description: {repo.Description}");
                }
                if (repo.LastSync.HasValue)
                {
                    Console.WriteLine($"    Last Sync: {repo.LastSync.Value:yyyy-MM-dd HH:mm:ss}");
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleRefreshIndex(IModIndexService modIndexService)
    {
        try
        {
            Console.WriteLine("Refreshing mod index...");
            
            var result = await modIndexService.RefreshIndex();
            
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

    private static async Task HandleInstallFromIndex(IGameDetector gameDetector, IModManager modManager, 
        IModIndexService modIndexService, string modId, string? path, string? version)
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

            Console.WriteLine($"Looking up mod: {modId}");
            var modEntry = await modIndexService.GetMod(modId);
            
            if (modEntry == null)
            {
                Console.WriteLine($"Error: Mod '{modId}' not found in index.");
                return;
            }

            Console.WriteLine($"Found mod: {modEntry.Name} by {modEntry.Author}");

            // find the version to install
            ModVersion? targetVersion = null;
            if (!string.IsNullOrEmpty(version))
            {
                targetVersion = modEntry.Versions.FirstOrDefault(v => v.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
                if (targetVersion == null)
                {
                    Console.WriteLine($"Error: Version '{version}' not found for mod '{modId}'.");
                    return;
                }
            }
            else
            {
                // use the latest non-prerelease version
                targetVersion = modEntry.Versions
                    .Where(v => !v.IsPrerelease)
                    .OrderByDescending(v => v.ReleaseDate)
                    .FirstOrDefault();
                
                if (targetVersion == null)
                {
                    Console.WriteLine($"Error: No stable version found for mod '{modId}'.");
                    return;
                }
            }

            Console.WriteLine($"Installing version: {targetVersion.Version}");
            Console.WriteLine($"Download URL: {targetVersion.DownloadUrl}");

            var result = await modManager.InstallModFromZip(targetVersion.DownloadUrl, gameInstall);
            
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

    private static async Task HandleVerifyChecksum(IVerificationService verificationService, string file, string expected, string algorithm)
    {
        try
        {
            Console.WriteLine($"Verifying {algorithm} checksum for: {file}");
            Console.WriteLine($"Expected: {expected}");
            
            var result = await verificationService.VerifyChecksum(file, expected, algorithm);
            
            if (result.Passed)
            {
                Console.WriteLine($"✓ Checksum verification successful");
                Console.WriteLine($"Details: {result.Details}");
            }
            else
            {
                Console.WriteLine($"✗ Checksum verification failed");
                Console.WriteLine($"Message: {result.Message}");
                Console.WriteLine($"Details: {result.Details}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleCheckPermissions(IGameDetector gameDetector, IPermissionsService permissionsService, string? path)
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

            Console.WriteLine($"Checking permissions for: {gameInstall.Name} at {gameInstall.InstallPath}");
            
            var permissions = await permissionsService.CheckPermissions(gameInstall);
            
            Console.WriteLine("Permission Status:");
            Console.WriteLine($"  Elevated: {(permissions.IsElevated ? "Yes" : "No")}");
            Console.WriteLine($"  Can write to game directory: {(permissions.CanWriteToGameDirectory ? "Yes" : "No")}");
            Console.WriteLine($"  Can create system files: {(permissions.CanCreateSystemFiles ? "Yes" : "No")}");
            Console.WriteLine($"  Can modify registry: {(permissions.CanModifyRegistry ? "Yes" : "No")}");
            Console.WriteLine($"  Requires elevation: {(permissions.RequiresElevation ? "Yes" : "No")}");
            
            if (permissions.RequiresElevation && !string.IsNullOrEmpty(permissions.ElevationReason))
            {
                Console.WriteLine($"  Elevation reason: {permissions.ElevationReason}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleCreateRestorePoint(IGameDetector gameDetector, IBackupManager backupManager, 
        string name, string? description, List<string>? tags, string? path)
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

            Console.WriteLine($"Creating restore point '{name}' for: {gameInstall.Name} at {gameInstall.InstallPath}");
            
            var result = await backupManager.CreateBackup(gameInstall, name);
            
            if (result.Success)
            {
                Console.WriteLine($"✓ {result.Message}");
                if (result.Data is string backupPath)
                {
                    var fileInfo = new FileInfo(backupPath);
                    Console.WriteLine($"  Backup: {backupPath}");
                    Console.WriteLine($"  Size: {fileInfo.Length:N0} bytes");
                    Console.WriteLine($"  Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}");
                }
            }
            else
            {
                Console.WriteLine($"✗ Error: {result.Message}");
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

    private static async Task HandleListRestorePoints(IGameDetector gameDetector, IBackupManager backupManager, string? path)
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

            Console.WriteLine($"Listing restore points for: {gameInstall.Name} at {gameInstall.InstallPath}");
            
            var restorePoints = await backupManager.ListBackups(gameInstall);
            
            if (restorePoints.Count == 0)
            {
                Console.WriteLine("No restore points found.");
                return;
            }

            Console.WriteLine($"Found {restorePoints.Count} backup(s):");
            Console.WriteLine();
            
            foreach (var backup in restorePoints)
            {
                Console.WriteLine($"Name: {backup.Name}");
                Console.WriteLine($"  Description: {backup.Description}");
                Console.WriteLine($"  Created: {backup.CreatedDate:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  Size: {backup.SizeBytes:N0} bytes");
                Console.WriteLine($"  Path: {backup.Path}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HandleRestoreFromPoint(IGameDetector gameDetector, IBackupManager backupManager, 
        string restorePointFile, string? path)
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

            Console.WriteLine($"Restoring from point: {restorePointFile}");
            Console.WriteLine($"Target: {gameInstall.Name} at {gameInstall.InstallPath}");
            
            var result = await backupManager.RestoreBackup(restorePointFile, gameInstall);
            
            if (result.Success)
            {
                Console.WriteLine($"✓ {result.Message}");
            }
            else
            {
                Console.WriteLine($"✗ Error: {result.Message}");
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
}

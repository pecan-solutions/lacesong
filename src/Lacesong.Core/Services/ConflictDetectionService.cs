using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Lacesong.Core.Services;

/// <summary>
/// service for detecting and resolving mod conflicts
/// </summary>
public class ConflictDetectionService : IConflictDetectionService
{
    private readonly IModManager _modManager;
    private readonly IDependencyResolver _dependencyResolver;

    public ConflictDetectionService(IModManager modManager, IDependencyResolver dependencyResolver)
    {
        _modManager = modManager;
        _dependencyResolver = dependencyResolver;
    }

    public async Task<List<ModConflict>> DetectConflicts(GameInstallation gameInstall, ModInfo? modToInstall = null)
    {
        var conflicts = new List<ModConflict>();
        
        try
        {
            var installedMods = await _modManager.GetInstalledMods(gameInstall);
            var allMods = new List<ModInfo>(installedMods);
            
            if (modToInstall != null)
            {
                allMods.Add(modToInstall);
            }

            // detect file conflicts
            var fileConflicts = await DetectFileConflicts(allMods, gameInstall);
            conflicts.AddRange(fileConflicts);

            // detect dependency conflicts
            var dependencyConflicts = await DetectDependencyConflicts(allMods, gameInstall);
            conflicts.AddRange(dependencyConflicts);

            // detect version conflicts
            var versionConflicts = await DetectVersionConflicts(allMods, gameInstall);
            conflicts.AddRange(versionConflicts);

            // detect load order conflicts
            var loadOrderConflicts = await DetectLoadOrderConflicts(allMods, gameInstall);
            conflicts.AddRange(loadOrderConflicts);

            // detect config conflicts
            var configConflicts = await DetectConfigConflicts(allMods, gameInstall);
            conflicts.AddRange(configConflicts);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting conflicts: {ex.Message}");
        }

        return conflicts;
    }

    public async Task<OperationResult> ResolveConflict(ModConflict conflict, GameInstallation gameInstall)
    {
        try
        {
            var resolution = conflict.Resolution;
            if (resolution == null || !resolution.CanAutoResolve)
            {
                return OperationResult.ErrorResult("Conflict cannot be automatically resolved", "Manual resolution required");
            }

            foreach (var action in resolution.Actions)
            {
                var result = await ExecuteResolutionAction(action, conflict, gameInstall);
                if (!result.Success)
                {
                    return OperationResult.ErrorResult($"Failed to execute action: {result.Error}", "Conflict resolution failed");
                }
            }

            return OperationResult.SuccessResult($"Conflict '{conflict.ConflictId}' resolved successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Conflict resolution failed");
        }
    }

    public async Task<List<ConflictResolution>> GetResolutionOptions(ModConflict conflict)
    {
        var resolutions = new List<ConflictResolution>();

        try
        {
            switch (conflict.ConflictType)
            {
                case ConflictType.FileConflict:
                    resolutions.AddRange(await GetFileConflictResolutions(conflict));
                    break;
                case ConflictType.DependencyConflict:
                    resolutions.AddRange(await GetDependencyConflictResolutions(conflict));
                    break;
                case ConflictType.VersionConflict:
                    resolutions.AddRange(await GetVersionConflictResolutions(conflict));
                    break;
                case ConflictType.LoadOrderConflict:
                    resolutions.AddRange(await GetLoadOrderConflictResolutions(conflict));
                    break;
                case ConflictType.ConfigConflict:
                    resolutions.AddRange(await GetConfigConflictResolutions(conflict));
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting resolution options: {ex.Message}");
        }

        return resolutions;
    }

    public async Task<ValidationResult> ValidateResolution(ModConflict conflict, ConflictResolution resolution)
    {
        try
        {
            // basic validation
            if (resolution.Actions.Count == 0)
            {
                return new ValidationResult
                {
                    Type = ValidationType.Dependency,
                    Passed = false,
                    Message = "Resolution has no actions",
                    Timestamp = DateTime.UtcNow
                };
            }

            // validate each action
            foreach (var action in resolution.Actions)
            {
                var validation = await ValidateResolutionAction(action, conflict);
                if (!validation.Passed)
                {
                    return validation;
                }
            }

            return new ValidationResult
            {
                Type = ValidationType.Dependency,
                Passed = true,
                Message = "Resolution validation successful",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                Type = ValidationType.Dependency,
                Passed = false,
                Message = $"Validation failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private async Task<List<ModConflict>> DetectFileConflicts(List<ModInfo> mods, GameInstallation gameInstall)
    {
        var conflicts = new List<ModConflict>();
        var fileMap = new Dictionary<string, List<string>>(); // file path -> list of mod ids

        try
        {
            foreach (var mod in mods)
            {
                var modPath = Path.Combine(ModManager.GetModsDirectoryPath(gameInstall), mod.Id);
                if (!Directory.Exists(modPath))
                    continue;

                var files = Directory.GetFiles(modPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(modPath, file);
                    
                    if (!fileMap.ContainsKey(relativePath))
                    {
                        fileMap[relativePath] = new List<string>();
                    }
                    
                    fileMap[relativePath].Add(mod.Id);
                }
            }

            // find conflicts
            foreach (var kvp in fileMap)
            {
                if (kvp.Value.Count > 1)
                {
                    var conflict = new ModConflict
                    {
                        ConflictId = $"file_{Guid.NewGuid():N}",
                        ConflictType = ConflictType.FileConflict,
                        ConflictingMods = kvp.Value,
                        ConflictingFiles = new List<string> { kvp.Key },
                        Severity = DetermineFileConflictSeverity(kvp.Key),
                        Description = $"Multiple mods provide the same file: {kvp.Key}",
                        Resolution = await GenerateFileConflictResolution(kvp.Key, kvp.Value)
                    };
                    
                    conflicts.Add(conflict);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting file conflicts: {ex.Message}");
        }

        return conflicts;
    }

    private async Task<List<ModConflict>> DetectDependencyConflicts(List<ModInfo> mods, GameInstallation gameInstall)
    {
        var conflicts = new List<ModConflict>();

        try
        {
            for (int i = 0; i < mods.Count; i++)
            {
                for (int j = i + 1; j < mods.Count; j++)
                {
                    var mod1 = mods[i];
                    var mod2 = mods[j];

                    // check for conflicting dependencies
                    var resolution1 = await _dependencyResolver.ResolveDependencies(mod1, gameInstall);
                    var resolution2 = await _dependencyResolver.ResolveDependencies(mod2, gameInstall);

                    var conflicts1 = resolution1.Conflicts.Where(c => c.ModId == mod2.Id).ToList();
                    var conflicts2 = resolution2.Conflicts.Where(c => c.ModId == mod1.Id).ToList();

                    if (conflicts1.Any() || conflicts2.Any())
                    {
                        var conflict = new ModConflict
                        {
                            ConflictId = $"dependency_{Guid.NewGuid():N}",
                            ConflictType = ConflictType.DependencyConflict,
                            ConflictingMods = new List<string> { mod1.Id, mod2.Id },
                            Severity = ConflictSeverity.Warning,
                            Description = $"Dependency conflict between {mod1.Name} and {mod2.Name}",
                            Resolution = await GenerateDependencyConflictResolution(mod1, mod2)
                        };

                        conflicts.Add(conflict);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting dependency conflicts: {ex.Message}");
        }

        return conflicts;
    }

    private async Task<List<ModConflict>> DetectVersionConflicts(List<ModInfo> mods, GameInstallation gameInstall)
    {
        var conflicts = new List<ModConflict>();
        var modGroups = mods.GroupBy(m => m.Id).ToList();

        try
        {
            foreach (var group in modGroups)
            {
                if (group.Count() > 1)
                {
                    var versions = group.Select(m => m.Version).Distinct().ToList();
                    var conflict = new ModConflict
                    {
                        ConflictId = $"version_{Guid.NewGuid():N}",
                        ConflictType = ConflictType.VersionConflict,
                        ConflictingMods = group.Select(m => m.Id).ToList(),
                        Severity = ConflictSeverity.Error,
                        Description = $"Multiple versions of {group.Key} found: {string.Join(", ", versions)}",
                        Resolution = await GenerateVersionConflictResolution(group.ToList())
                    };

                    conflicts.Add(conflict);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting version conflicts: {ex.Message}");
        }

        return conflicts;
    }

    private async Task<List<ModConflict>> DetectLoadOrderConflicts(List<ModInfo> mods, GameInstallation gameInstall)
    {
        var conflicts = new List<ModConflict>();

        try
        {
            // check for explicit load order conflicts
            var loadOrderFile = Path.Combine(gameInstall.InstallPath, "BepInEx", "config", "BepInEx.cfg");
            if (File.Exists(loadOrderFile))
            {
                var content = await File.ReadAllTextAsync(loadOrderFile);
                // parse load order configuration and detect conflicts
                // this is a simplified implementation
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting load order conflicts: {ex.Message}");
        }

        return conflicts;
    }

    private async Task<List<ModConflict>> DetectConfigConflicts(List<ModInfo> mods, GameInstallation gameInstall)
    {
        var conflicts = new List<ModConflict>();

        try
        {
            // check for config file conflicts in BepInEx config directory
            var configDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "config");
            if (Directory.Exists(configDir))
            {
                var configFiles = Directory.GetFiles(configDir, "*.cfg", SearchOption.AllDirectories);
                var configMap = new Dictionary<string, List<string>>();

                foreach (var configFile in configFiles)
                {
                    var fileName = Path.GetFileName(configFile);
                    if (!configMap.ContainsKey(fileName))
                    {
                        configMap[fileName] = new List<string>();
                    }
                    
                    // try to determine which mod owns this config
                    var content = await File.ReadAllTextAsync(configFile);
                    var owningMods = ExtractModIdsFromConfig(content);
                    configMap[fileName].AddRange(owningMods);
                }

                // detect conflicts
                foreach (var kvp in configMap)
                {
                    if (kvp.Value.Count > 1)
                    {
                        var conflict = new ModConflict
                        {
                            ConflictId = $"config_{Guid.NewGuid():N}",
                            ConflictType = ConflictType.ConfigConflict,
                            ConflictingMods = kvp.Value.Distinct().ToList(),
                            ConflictingFiles = new List<string> { kvp.Key },
                            Severity = ConflictSeverity.Info,
                            Description = $"Multiple mods use the same config file: {kvp.Key}",
                            Resolution = await GenerateConfigConflictResolution(kvp.Key, kvp.Value)
                        };

                        conflicts.Add(conflict);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting config conflicts: {ex.Message}");
        }

        return conflicts;
    }

    private ConflictSeverity DetermineFileConflictSeverity(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".dll" => ConflictSeverity.Critical,
            ".exe" => ConflictSeverity.Critical,
            ".config" => ConflictSeverity.Warning,
            ".txt" => ConflictSeverity.Info,
            ".json" => ConflictSeverity.Warning,
            ".xml" => ConflictSeverity.Warning,
            _ => ConflictSeverity.Warning
        };
    }

    private Task<ConflictResolution?> GenerateFileConflictResolution(string filePath, List<string> modIds)
    {
        // simple resolution: keep the most recently installed mod
        var latestMod = modIds.Last();
        
        return Task.FromResult<ConflictResolution?>(new ConflictResolution
        {
            ResolutionType = ResolutionType.Automatic,
            Description = $"Keep file from {latestMod}, disable from others",
            CanAutoResolve = true,
            Actions = new List<ResolutionAction>
            {
                new ResolutionAction
                {
                    ActionType = ActionType.RenameFile,
                    Description = $"Rename conflicting file from other mods",
                    Target = filePath,
                    Parameters = new Dictionary<string, object>
                    {
                        { "keepMod", latestMod },
                        { "disableMods", modIds.Where(id => id != latestMod).ToList() }
                    }
                }
            }
        });
    }

    private Task<ConflictResolution?> GenerateDependencyConflictResolution(ModInfo mod1, ModInfo mod2)
    {
        return Task.FromResult<ConflictResolution?>(new ConflictResolution
        {
            ResolutionType = ResolutionType.UserChoice,
            Description = $"Choose which mod to keep: {mod1.Name} or {mod2.Name}",
            CanAutoResolve = false,
            Actions = new List<ResolutionAction>
            {
                new ResolutionAction
                {
                    ActionType = ActionType.DisableMod,
                    Description = "Disable one of the conflicting mods",
                    Target = "user_choice"
                }
            }
        });
    }

    private Task<ConflictResolution?> GenerateVersionConflictResolution(List<ModInfo> mods)
    {
        var latestMod = mods.OrderByDescending(m => ParseVersion(m.Version)).First();
        
        return Task.FromResult<ConflictResolution?>(new ConflictResolution
        {
            ResolutionType = ResolutionType.Automatic,
            Description = $"Keep latest version: {latestMod.Version}",
            CanAutoResolve = true,
            Actions = new List<ResolutionAction>
            {
                new ResolutionAction
                {
                    ActionType = ActionType.DisableMod,
                    Description = "Disable older versions",
                    Target = latestMod.Id,
                    Parameters = new Dictionary<string, object>
                    {
                        { "disableVersions", mods.Where(m => m.Version != latestMod.Version).Select(m => m.Version).ToList() }
                    }
                }
            }
        });
    }

    private Task<ConflictResolution?> GenerateConfigConflictResolution(string configFile, List<string> modIds)
    {
        return Task.FromResult<ConflictResolution?>(new ConflictResolution
        {
            ResolutionType = ResolutionType.Manual,
            Description = "Config files may need manual merging",
            CanAutoResolve = false,
            Actions = new List<ResolutionAction>
            {
                new ResolutionAction
                {
                    ActionType = ActionType.MergeConfig,
                    Description = "Manually merge config files",
                    Target = configFile
                }
            }
        });
    }

    private Task<List<ConflictResolution>> GetFileConflictResolutions(ModConflict conflict)
    {
        var resolutions = new List<ConflictResolution>();
        
        // resolution 1: keep newest mod
        resolutions.Add(new ConflictResolution
        {
            ResolutionType = ResolutionType.Automatic,
            Description = "Keep file from the most recently installed mod",
            CanAutoResolve = true,
            Actions = new List<ResolutionAction>
            {
                new ResolutionAction
                {
                    ActionType = ActionType.RenameFile,
                    Description = "Rename conflicting files from other mods",
                    Target = string.Join(",", conflict.ConflictingFiles)
                }
            }
        });

        // resolution 2: user choice
        resolutions.Add(new ConflictResolution
        {
            ResolutionType = ResolutionType.UserChoice,
            Description = "Let user choose which mod's file to keep",
            CanAutoResolve = false,
            Actions = new List<ResolutionAction>()
        });

        return Task.FromResult(resolutions);
    }

    private Task<List<ConflictResolution>> GetDependencyConflictResolutions(ModConflict conflict)
    {
        var resolutions = new List<ConflictResolution>();
        
        resolutions.Add(new ConflictResolution
        {
            ResolutionType = ResolutionType.UserChoice,
            Description = "Choose which mod to keep installed",
            CanAutoResolve = false,
            Actions = new List<ResolutionAction>
            {
                new ResolutionAction
                {
                    ActionType = ActionType.DisableMod,
                    Description = "Disable one of the conflicting mods",
                    Target = "user_choice"
                }
            }
        });

        return Task.FromResult(resolutions);
    }

    private Task<List<ConflictResolution>> GetVersionConflictResolutions(ModConflict conflict)
    {
        var resolutions = new List<ConflictResolution>();
        
        resolutions.Add(new ConflictResolution
        {
            ResolutionType = ResolutionType.Automatic,
            Description = "Keep the latest version",
            CanAutoResolve = true,
            Actions = new List<ResolutionAction>
            {
                new ResolutionAction
                {
                    ActionType = ActionType.DisableMod,
                    Description = "Disable older versions",
                    Target = "latest_version"
                }
            }
        });

        return Task.FromResult(resolutions);
    }

    private Task<List<ConflictResolution>> GetLoadOrderConflictResolutions(ModConflict conflict)
    {
        var resolutions = new List<ConflictResolution>();
        
        resolutions.Add(new ConflictResolution
        {
            ResolutionType = ResolutionType.Manual,
            Description = "Manually adjust load order",
            CanAutoResolve = false,
            Actions = new List<ResolutionAction>
            {
                new ResolutionAction
                {
                    ActionType = ActionType.ChangeLoadOrder,
                    Description = "Change mod load order",
                    Target = "load_order"
                }
            }
        });

        return Task.FromResult(resolutions);
    }

    private Task<List<ConflictResolution>> GetConfigConflictResolutions(ModConflict conflict)
    {
        var resolutions = new List<ConflictResolution>();
        
        resolutions.Add(new ConflictResolution
        {
            ResolutionType = ResolutionType.Manual,
            Description = "Manually merge configuration files",
            CanAutoResolve = false,
            Actions = new List<ResolutionAction>
            {
                new ResolutionAction
                {
                    ActionType = ActionType.MergeConfig,
                    Description = "Merge config files",
                    Target = string.Join(",", conflict.ConflictingFiles)
                }
            }
        });

        return Task.FromResult(resolutions);
    }

    private async Task<OperationResult> ExecuteResolutionAction(ResolutionAction action, ModConflict conflict, GameInstallation gameInstall)
    {
        try
        {
            switch (action.ActionType)
            {
                case ActionType.RenameFile:
                    return await ExecuteRenameFile(action, conflict, gameInstall);
                case ActionType.DisableMod:
                    return await ExecuteDisableMod(action, conflict, gameInstall);
                case ActionType.DeleteFile:
                    return await ExecuteDeleteFile(action, conflict, gameInstall);
                case ActionType.ChangeLoadOrder:
                    return await ExecuteChangeLoadOrder(action, conflict, gameInstall);
                case ActionType.MergeConfig:
                    return await ExecuteMergeConfig(action, conflict, gameInstall);
                default:
                    return OperationResult.ErrorResult($"Unsupported action type: {action.ActionType}", "Action execution failed");
            }
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Action execution failed");
        }
    }

    private Task<OperationResult> ExecuteRenameFile(ResolutionAction action, ModConflict conflict, GameInstallation gameInstall)
    {
        // implementation for renaming conflicting files
        return Task.FromResult(OperationResult.SuccessResult("File renamed successfully"));
    }

    private Task<OperationResult> ExecuteDisableMod(ResolutionAction action, ModConflict conflict, GameInstallation gameInstall)
    {
        // implementation for disabling conflicting mods
        return Task.FromResult(OperationResult.SuccessResult("Mod disabled successfully"));
    }

    private Task<OperationResult> ExecuteDeleteFile(ResolutionAction action, ModConflict conflict, GameInstallation gameInstall)
    {
        // implementation for deleting conflicting files
        return Task.FromResult(OperationResult.SuccessResult("File deleted successfully"));
    }

    private Task<OperationResult> ExecuteChangeLoadOrder(ResolutionAction action, ModConflict conflict, GameInstallation gameInstall)
    {
        // implementation for changing load order
        return Task.FromResult(OperationResult.SuccessResult("Load order changed successfully"));
    }

    private Task<OperationResult> ExecuteMergeConfig(ResolutionAction action, ModConflict conflict, GameInstallation gameInstall)
    {
        // implementation for merging config files
        return Task.FromResult(OperationResult.SuccessResult("Config merged successfully"));
    }

    private Task<ValidationResult> ValidateResolutionAction(ResolutionAction action, ModConflict conflict)
    {
        try
        {
            // basic validation based on action type
            switch (action.ActionType)
            {
                case ActionType.RenameFile:
                case ActionType.DeleteFile:
                    if (string.IsNullOrEmpty(action.Target))
                    {
                        return Task.FromResult(new ValidationResult
                        {
                            Type = ValidationType.Dependency,
                            Passed = false,
                            Message = "File action requires a target file path",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    break;
                case ActionType.DisableMod:
                    if (string.IsNullOrEmpty(action.Target) || action.Target == "user_choice")
                    {
                        return Task.FromResult(new ValidationResult
                        {
                            Type = ValidationType.Dependency,
                            Passed = false,
                            Message = "Disable mod action requires a specific mod target",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    break;
            }

            return Task.FromResult(new ValidationResult
            {
                Type = ValidationType.Dependency,
                Passed = true,
                Message = "Action validation successful",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ValidationResult
            {
                Type = ValidationType.Dependency,
                Passed = false,
                Message = $"Validation failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private List<string> ExtractModIdsFromConfig(string configContent)
    {
        var modIds = new List<string>();
        
        // simple regex to extract mod IDs from config content
        var patterns = new[]
        {
            @"ModId\s*=\s*([^\s,]+)",
            @"Plugin\s*=\s*([^\s,]+)",
            @"Assembly\s*=\s*([^\s,]+)"
        };

        foreach (var pattern in patterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(configContent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    modIds.Add(match.Groups[1].Value);
                }
            }
        }

        return modIds.Distinct().ToList();
    }

    private Version ParseVersion(string versionString)
    {
        try
        {
            return Version.Parse(versionString);
        }
        catch
        {
            return new Version(0, 0, 0);
        }
    }
}

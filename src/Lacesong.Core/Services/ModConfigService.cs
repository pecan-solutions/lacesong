using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Lacesong.Core.Services;

/// <summary>
/// service for managing mod configuration files with preservation and merging capabilities
/// </summary>
public class ModConfigService : IModConfigService
{
    private readonly IVerificationService _verificationService;

    public ModConfigService(IVerificationService verificationService)
    {
        _verificationService = verificationService;
    }

    public async Task<List<ModConfig>> GetModConfigs(string modId, GameInstallation gameInstall)
    {
        var configs = new List<ModConfig>();
        
        try
        {
            var configDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "config");
            if (!Directory.Exists(configDir))
                return configs;

            // look for config files related to this mod
            var configFiles = Directory.GetFiles(configDir, "*", SearchOption.AllDirectories)
                .Where(f => IsModConfigFile(f, modId))
                .ToList();

            foreach (var configFile in configFiles)
            {
                var config = await CreateModConfigFromFile(configFile, modId);
                if (config != null)
                {
                    configs.Add(config);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting mod configs: {ex.Message}");
        }

        return configs;
    }

    public async Task<OperationResult> BackupModConfigs(string modId, GameInstallation gameInstall)
    {
        try
        {
            var configs = await GetModConfigs(modId, gameInstall);
            if (configs.Count == 0)
            {
                return OperationResult.SuccessResult("No config files to backup");
            }

            var backupDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "backups", "configs", modId);
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupDir, $"config_backup_{timestamp}");

            Directory.CreateDirectory(backupPath);

            foreach (var config in configs)
            {
                var fileName = Path.GetFileName(config.ConfigPath);
                var backupFilePath = Path.Combine(backupPath, fileName);
                
                File.Copy(config.ConfigPath, backupFilePath, true);
                
                // update backup path in config object
                config.BackupPath = backupFilePath;
            }

            // save backup manifest
            var manifestPath = Path.Combine(backupPath, "backup_manifest.json");
            var manifest = new
            {
                ModId = modId,
                CreatedDate = DateTime.UtcNow,
                Configs = configs,
                GameInstall = gameInstall
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, manifestJson);

            return OperationResult.SuccessResult($"Backed up {configs.Count} config files", backupPath);
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to backup mod configs");
        }
    }

    public async Task<OperationResult> RestoreModConfigs(string modId, GameInstallation gameInstall, string? backupPath = null)
    {
        try
        {
            var backupDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "backups", "configs", modId);
            
            if (string.IsNullOrEmpty(backupPath))
            {
                // find latest backup
                if (!Directory.Exists(backupDir))
                {
                    return OperationResult.ErrorResult("No backup directory found", "No backups available");
                }

                var backupDirs = Directory.GetDirectories(backupDir)
                    .OrderByDescending(d => Directory.GetCreationTime(d))
                    .ToList();

                if (backupDirs.Count == 0)
                {
                    return OperationResult.ErrorResult("No backups found", "No backups available");
                }

                backupPath = backupDirs.First();
            }

            if (!Directory.Exists(backupPath))
            {
                return OperationResult.ErrorResult("Backup path does not exist", "Invalid backup path");
            }

            // load backup manifest
            var manifestPath = Path.Combine(backupPath, "backup_manifest.json");
            if (!File.Exists(manifestPath))
            {
                return OperationResult.ErrorResult("Backup manifest not found", "Invalid backup");
            }

            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<JsonElement>(manifestJson);

            // restore config files
            var configs = JsonSerializer.Deserialize<List<ModConfig>>(manifest.GetProperty("Configs"));
            if (configs == null || configs.Count == 0)
            {
                return OperationResult.SuccessResult("No config files to restore");
            }

            foreach (var config in configs)
            {
                var backupFilePath = Path.Combine(backupPath, Path.GetFileName(config.ConfigPath));
                if (File.Exists(backupFilePath))
                {
                    File.Copy(backupFilePath, config.ConfigPath, true);
                }
            }

            return OperationResult.SuccessResult($"Restored {configs.Count} config files");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to restore mod configs");
        }
    }

    public async Task<OperationResult> MergeConfigs(string modId, List<ModConfig> oldConfigs, List<ModConfig> newConfigs, GameInstallation gameInstall)
    {
        try
        {
            var mergedConfigs = new List<ModConfig>();

            foreach (var newConfig in newConfigs)
            {
                var oldConfig = oldConfigs.FirstOrDefault(oc => 
                    Path.GetFileName(oc.ConfigPath) == Path.GetFileName(newConfig.ConfigPath));

                if (oldConfig == null)
                {
                    // new config file, just use it
                    mergedConfigs.Add(newConfig);
                }
                else
                {
                    // merge existing config
                    var mergedConfig = await MergeConfigFile(oldConfig, newConfig, gameInstall);
                    if (mergedConfig != null)
                    {
                        mergedConfigs.Add(mergedConfig);
                    }
                    else
                    {
                        // fallback to new config if merge fails
                        mergedConfigs.Add(newConfig);
                    }
                }
            }

            // copy merged configs to target location
            foreach (var config in mergedConfigs)
            {
                var targetPath = GetConfigTargetPath(config, gameInstall);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                
                File.Copy(config.ConfigPath, targetPath, true);
            }

            return OperationResult.SuccessResult($"Merged {mergedConfigs.Count} config files");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to merge configs");
        }
    }

    public async Task<bool> IsConfigModified(string configPath, string originalChecksum)
    {
        try
        {
            if (!File.Exists(configPath))
                return false;

            var currentChecksum = await _verificationService.CalculateChecksum(configPath);
            return currentChecksum != originalChecksum;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking config modification: {ex.Message}");
            return false;
        }
    }

    private async Task<ModConfig?> CreateModConfigFromFile(string configPath, string modId)
    {
        try
        {
            if (!File.Exists(configPath))
                return null;

            var fileInfo = new FileInfo(configPath);
            var configType = DetermineConfigType(configPath);
            var checksum = await _verificationService.CalculateChecksum(configPath);

            return new ModConfig
            {
                ModId = modId,
                ConfigPath = configPath,
                ConfigType = configType,
                LastModified = fileInfo.LastWriteTime,
                Size = fileInfo.Length,
                Checksum = checksum,
                IsUserModified = await IsConfigModified(configPath, checksum) // this will be false for original files
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating mod config from file: {ex.Message}");
            return null;
        }
    }

    private ConfigType DetermineConfigType(string configPath)
    {
        var extension = Path.GetExtension(configPath).ToLowerInvariant();
        
        return extension switch
        {
            ".json" => ConfigType.Json,
            ".yaml" or ".yml" => ConfigType.Yaml,
            ".ini" => ConfigType.Ini,
            ".xml" => ConfigType.Xml,
            ".toml" => ConfigType.Toml,
            ".properties" => ConfigType.Properties,
            _ => ConfigType.Unknown
        };
    }

    private bool IsModConfigFile(string configPath, string modId)
    {
        var fileName = Path.GetFileNameWithoutExtension(configPath).ToLowerInvariant();
        var modIdLower = modId.ToLowerInvariant();
        
        // check if filename contains mod id
        if (fileName.Contains(modIdLower))
            return true;

        // check file content for mod references
        try
        {
            var content = File.ReadAllText(configPath, Encoding.UTF8).ToLowerInvariant();
            return content.Contains(modIdLower) || 
                   content.Contains($"plugin={modIdLower}") ||
                   content.Contains($"assembly={modIdLower}");
        }
        catch
        {
            return false;
        }
    }

    private async Task<ModConfig?> MergeConfigFile(ModConfig oldConfig, ModConfig newConfig, GameInstallation gameInstall)
    {
        try
        {
            var oldContent = await File.ReadAllTextAsync(oldConfig.ConfigPath);
            var newContent = await File.ReadAllTextAsync(newConfig.ConfigPath);

            string mergedContent;
            
            switch (newConfig.ConfigType)
            {
                case ConfigType.Json:
                    mergedContent = await MergeJsonConfig(oldContent, newContent);
                    break;
                case ConfigType.Yaml:
                    mergedContent = await MergeYamlConfig(oldContent, newContent);
                    break;
                case ConfigType.Ini:
                    mergedContent = await MergeIniConfig(oldContent, newContent);
                    break;
                case ConfigType.Xml:
                    mergedContent = await MergeXmlConfig(oldContent, newContent);
                    break;
                default:
                    // for unknown types, prefer user's version if modified, otherwise use new version
                    mergedContent = await IsConfigModified(oldConfig.ConfigPath, oldConfig.Checksum ?? "") 
                        ? oldContent 
                        : newContent;
                    break;
            }

            // create temporary merged file
            var tempPath = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempPath, mergedContent);

            var mergedConfig = new ModConfig
            {
                ModId = newConfig.ModId,
                ConfigPath = tempPath,
                ConfigType = newConfig.ConfigType,
                LastModified = DateTime.UtcNow,
                Size = new FileInfo(tempPath).Length,
                Checksum = await _verificationService.CalculateChecksum(tempPath),
                IsUserModified = true // merged configs are considered user-modified
            };

            return mergedConfig;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error merging config file: {ex.Message}");
            return null;
        }
    }

    private Task<string> MergeJsonConfig(string oldContent, string newContent)
    {
        try
        {
            var oldJson = JsonSerializer.Deserialize<JsonElement>(oldContent);
            var newJson = JsonSerializer.Deserialize<JsonElement>(newContent);

            var mergedJson = MergeJsonElements(oldJson, newJson);
            
            return Task.FromResult(JsonSerializer.Serialize(mergedJson, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error merging JSON config: {ex.Message}");
            return Task.FromResult(newContent); // fallback to new content
        }
    }

    private JsonElement MergeJsonElements(JsonElement oldElement, JsonElement newElement)
    {
        // simple JSON merging - prefer old values for user modifications
        // this is a basic implementation and could be enhanced
        return newElement; // for now, prefer new values
    }

    private Task<string> MergeYamlConfig(string oldContent, string newContent)
    {
        // yaml merging is complex, for now just return old content if it was user-modified
        // in a full implementation, you'd use a YAML library like YamlDotNet
        return Task.FromResult(oldContent); // fallback to old content
    }

    private Task<string> MergeIniConfig(string oldContent, string newContent)
    {
        try
        {
            var oldLines = oldContent.Split('\n');
            var newLines = newContent.Split('\n');
            
            var mergedLines = new List<string>();
            var oldSections = ParseIniSections(oldLines);
            var newSections = ParseIniSections(newLines);

            // merge sections
            foreach (var newSection in newSections)
            {
                var sectionName = newSection.Key;
                var newSectionContent = newSection.Value;
                
                if (oldSections.ContainsKey(sectionName))
                {
                    var oldSectionContent = oldSections[sectionName];
                    var mergedSection = MergeIniSection(oldSectionContent, newSectionContent);
                    mergedLines.Add($"[{sectionName}]");
                    mergedLines.AddRange(mergedSection);
                }
                else
                {
                    mergedLines.Add($"[{sectionName}]");
                    mergedLines.AddRange(newSectionContent);
                }
            }

            return Task.FromResult(string.Join('\n', mergedLines));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error merging INI config: {ex.Message}");
            return Task.FromResult(newContent); // fallback to new content
        }
    }

    private Dictionary<string, List<string>> ParseIniSections(string[] lines)
    {
        var sections = new Dictionary<string, List<string>>();
        var currentSection = "";
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
            {
                currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                sections[currentSection] = new List<string>();
            }
            else if (!string.IsNullOrEmpty(trimmedLine) && !string.IsNullOrEmpty(currentSection))
            {
                sections[currentSection].Add(line);
            }
        }
        
        return sections;
    }

    private List<string> MergeIniSection(List<string> oldSection, List<string> newSection)
    {
        var mergedLines = new List<string>();
        var oldKeys = new Dictionary<string, string>();
        
        // parse old section keys
        foreach (var line in oldSection)
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                oldKeys[parts[0].Trim()] = line;
            }
            else
            {
                mergedLines.Add(line); // comments and other lines
            }
        }
        
        // merge with new section
        foreach (var line in newSection)
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                if (oldKeys.ContainsKey(key))
                {
                    // prefer old value (user modification)
                    mergedLines.Add(oldKeys[key]);
                    oldKeys.Remove(key);
                }
                else
                {
                    mergedLines.Add(line);
                }
            }
            else
            {
                mergedLines.Add(line);
            }
        }
        
        // add remaining old keys
        mergedLines.AddRange(oldKeys.Values);
        
        return mergedLines;
    }

    private Task<string> MergeXmlConfig(string oldContent, string newContent)
    {
        // XML merging is complex, for now just return old content if it was user-modified
        // in a full implementation, you'd use XML libraries for proper merging
        return Task.FromResult(oldContent); // fallback to old content
    }

    private string GetConfigTargetPath(ModConfig config, GameInstallation gameInstall)
    {
        // determine where to place the config file in the game installation
        var configDir = Path.Combine(gameInstall.InstallPath, "BepInEx", "config");
        var fileName = Path.GetFileName(config.ConfigPath);
        return Path.Combine(configDir, fileName);
    }
}

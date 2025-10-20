using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;

namespace Lacesong.Core.Services;

/// <summary>
/// service for managing doorstop configuration on windows
/// </summary>
public class DoorstopConfigManager : IDoorstopConfigManager
{
    private const string DoorstopConfigFileName = "doorstop_config.ini";
    private const string EnabledKey = "enabled";
    private const string EnabledTrue = "true";
    private const string EnabledFalse = "false";

    public bool IsDoorstopInstalled(GameInstallation gameInstall)
    {
        try
        {
            var configPath = GetDoorstopConfigPath(gameInstall);
            return File.Exists(configPath);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsDoorstopEnabled(GameInstallation gameInstall)
    {
        try
        {
            var configPath = GetDoorstopConfigPath(gameInstall);
            if (!File.Exists(configPath))
            {
                return false; // if no config file, assume disabled
            }

            var content = await File.ReadAllTextAsync(configPath);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith($"{EnabledKey}=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = trimmedLine.Substring(EnabledKey.Length + 1).Trim();
                    return string.Equals(value, EnabledTrue, StringComparison.OrdinalIgnoreCase);
                }
            }

            return false; // if enabled key not found, assume disabled
        }
        catch
        {
            return false;
        }
    }

    public async Task<OperationResult> SetDoorstopEnabled(GameInstallation gameInstall, bool enabled)
    {
        try
        {
            var configPath = GetDoorstopConfigPath(gameInstall);
            var enabledValue = enabled ? EnabledTrue : EnabledFalse;
            
            // read existing config or create new one
            var configContent = new List<string>();
            bool foundEnabledKey = false;

            if (File.Exists(configPath))
            {
                var existingContent = await File.ReadAllTextAsync(configPath);
                var lines = existingContent.Split('\n');
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith($"{EnabledKey}=", StringComparison.OrdinalIgnoreCase))
                    {
                        configContent.Add($"{EnabledKey}={enabledValue}");
                        foundEnabledKey = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        configContent.Add(line); // preserve original formatting
                    }
                }
            }

            // if enabled key wasn't found, add it
            if (!foundEnabledKey)
            {
                configContent.Add($"{EnabledKey}={enabledValue}");
            }

            // write the config file
            await File.WriteAllTextAsync(configPath, string.Join(Environment.NewLine, configContent));
            
            return OperationResult.SuccessResult($"Doorstop {(enabled ? "enabled" : "disabled")} successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult($"Failed to set Doorstop enabled state: {ex.Message}", "Configuration failed");
        }
    }

    public async Task<OperationResult> DisableDoorstop(GameInstallation gameInstall)
    {
        return await SetDoorstopEnabled(gameInstall, false);
    }

    public async Task<OperationResult> EnableDoorstop(GameInstallation gameInstall)
    {
        return await SetDoorstopEnabled(gameInstall, true);
    }

    private string GetDoorstopConfigPath(GameInstallation gameInstall)
    {
        return Path.Combine(gameInstall.InstallPath, DoorstopConfigFileName);
    }
}

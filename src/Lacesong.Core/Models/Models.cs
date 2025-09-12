using System.Text.Json.Serialization;

namespace Lacesong.Core.Models;

/// <summary>
/// represents a game installation with its metadata
/// </summary>
public class GameInstallation
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("installPath")]
    public string InstallPath { get; set; } = string.Empty;

    [JsonPropertyName("executable")]
    public string Executable { get; set; } = string.Empty;

    [JsonPropertyName("steamAppId")]
    public string? SteamAppId { get; set; }

    [JsonPropertyName("epicAppId")]
    public string? EpicAppId { get; set; }

    [JsonPropertyName("gogAppId")]
    public string? GogAppId { get; set; }

    [JsonPropertyName("bepInExVersion")]
    public string BepInExVersion { get; set; } = string.Empty;

    [JsonPropertyName("modDirectory")]
    public string ModDirectory { get; set; } = string.Empty;

    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("detectedBy")]
    public string DetectedBy { get; set; } = string.Empty;
}

/// <summary>
/// represents a mod with its metadata and installation status
/// </summary>
public class ModInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("isInstalled")]
    public bool IsInstalled { get; set; }

    [JsonPropertyName("installDate")]
    public DateTime? InstallDate { get; set; }
}

/// <summary>
/// represents installation options for bepinex
/// </summary>
public class BepInExInstallOptions
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("forceReinstall")]
    public bool ForceReinstall { get; set; }

    [JsonPropertyName("backupExisting")]
    public bool BackupExisting { get; set; } = true;

    [JsonPropertyName("createDesktopShortcut")]
    public bool CreateDesktopShortcut { get; set; } = false;
}

/// <summary>
/// represents the result of an operation
/// </summary>
public class OperationResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    public static OperationResult SuccessResult(string message = "Operation completed successfully", object? data = null)
    {
        return new OperationResult { Success = true, Message = message, Data = data };
    }

    public static OperationResult ErrorResult(string error, string message = "Operation failed")
    {
        return new OperationResult { Success = false, Message = message, Error = error };
    }
}

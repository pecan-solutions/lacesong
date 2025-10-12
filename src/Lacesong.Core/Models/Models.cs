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

    [JsonPropertyName("xboxAppId")]
    public string? XboxAppId { get; set; }

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

    // optional website/homepage for the mod
    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    // additional tags / categories supplied by the manifest (e.g., ["visual","cosmetic"])
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("iconPath")]
    public string? IconPath { get; set; }

    [JsonPropertyName("isInstalled")]
    public bool IsInstalled { get; set; }

    [JsonPropertyName("compatibilityStatus")]
    public CompatibilityStatus CompatibilityStatus { get; set; } = CompatibilityStatus.Unknown;

    // semver constraint string, e.g., ">=1.0.0 <2.0.0"
    [JsonPropertyName("versionConstraints")]
    public string? VersionConstraints { get; set; }

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

    [JsonPropertyName("verifySignature")]
    public bool VerifySignature { get; set; } = true;

    [JsonPropertyName("verifyChecksum")]
    public bool VerifyChecksum { get; set; } = true;

    [JsonPropertyName("requireElevation")]
    public bool RequireElevation { get; set; } = false;
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

/// <summary>
/// represents backup information
/// </summary>
public class BackupInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public long SizeBytes { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// represents a mod index entry with version information
/// </summary>
public class ModIndexEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("repository")]
    public string Repository { get; set; } = string.Empty;

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    [JsonPropertyName("gameCompatibility")]
    public List<string> GameCompatibility { get; set; } = new();

    [JsonPropertyName("versions")]
    public List<ModVersion> Versions { get; set; } = new();

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("conflicts")]
    public List<string> Conflicts { get; set; } = new();

    [JsonPropertyName("isOfficial")]
    public bool IsOfficial { get; set; } = false;

    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; } = false;

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("downloadCount")]
    public long DownloadCount { get; set; } = 0;

    [JsonPropertyName("rating")]
    public double Rating { get; set; } = 0.0;

    [JsonPropertyName("ratingCount")]
    public int RatingCount { get; set; } = 0;

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

/// <summary>
/// represents a specific version of a mod
/// </summary>
public class ModVersion
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; set; }

    [JsonPropertyName("checksumType")]
    public string ChecksumType { get; set; } = "SHA256";

    [JsonPropertyName("releaseDate")]
    public DateTime ReleaseDate { get; set; }

    [JsonPropertyName("changelog")]
    public string? Changelog { get; set; }

    [JsonPropertyName("isPrerelease")]
    public bool IsPrerelease { get; set; } = false;

    [JsonPropertyName("gameVersion")]
    public string? GameVersion { get; set; }

    [JsonPropertyName("bepInExVersion")]
    public string? BepInExVersion { get; set; }

    [JsonPropertyName("dependencies")]
    public List<ModDependency> Dependencies { get; set; } = new();

    [JsonPropertyName("conflicts")]
    public List<string> Conflicts { get; set; } = new();
}

/// <summary>
/// represents a mod dependency
/// </summary>
public class ModDependency
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("isOptional")]
    public bool IsOptional { get; set; } = false;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// represents the complete mod index
/// </summary>
public class ModIndex
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("totalMods")]
    public int TotalMods { get; set; }

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();

    [JsonPropertyName("mods")]
    public List<ModIndexEntry> Mods { get; set; } = new();

    [JsonPropertyName("repositories")]
    public List<ModRepository> Repositories { get; set; } = new();
}

/// <summary>
/// represents a mod repository configuration
/// </summary>
public class ModRepository
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "GitHub"; // GitHub, GitLab, Custom

    [JsonPropertyName("isOfficial")]
    public bool IsOfficial { get; set; } = false;

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("lastSync")]
    public DateTime? LastSync { get; set; }

    [JsonPropertyName("syncInterval")]
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromHours(6);

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// represents search criteria for mod index
/// </summary>
public class ModSearchCriteria
{
    [JsonPropertyName("query")]
    public string? Query { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("isOfficial")]
    public bool? IsOfficial { get; set; }

    [JsonPropertyName("isVerified")]
    public bool? IsVerified { get; set; }

    [JsonPropertyName("gameCompatibility")]
    public List<string> GameCompatibility { get; set; } = new();

    [JsonPropertyName("sortBy")]
    public string SortBy { get; set; } = "name"; // name, date, downloads, rating

    [JsonPropertyName("sortOrder")]
    public string SortOrder { get; set; } = "asc"; // asc, desc

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// represents search results from mod index
/// </summary>
public class ModSearchResults
{
    [JsonPropertyName("mods")]
    public List<ModIndexEntry> Mods { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("searchTime")]
    public TimeSpan SearchTime { get; set; }
}

/// <summary>
/// represents a file signature for verification
/// </summary>
public class FileSignature
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "SHA256";

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("publicKey")]
    public string? PublicKey { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }
}

/// <summary>
/// represents dependency resolution result
/// </summary>
public class DependencyResolution
{
    [JsonPropertyName("resolved")]
    public List<ModDependency> Resolved { get; set; } = new();

    [JsonPropertyName("conflicts")]
    public List<DependencyConflict> Conflicts { get; set; } = new();

    [JsonPropertyName("missing")]
    public List<ModDependency> Missing { get; set; } = new();

    [JsonPropertyName("bepInExVersion")]
    public string? BepInExVersion { get; set; }

    [JsonPropertyName("bepInExCompatible")]
    public bool BepInExCompatible { get; set; } = true;

    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; } = true;
}

/// <summary>
/// represents a dependency conflict
/// </summary>
public class DependencyConflict
{
    [JsonPropertyName("modId")]
    public string ModId { get; set; } = string.Empty;

    [JsonPropertyName("requiredVersion")]
    public string? RequiredVersion { get; set; }

    [JsonPropertyName("installedVersion")]
    public string? InstalledVersion { get; set; }

    [JsonPropertyName("conflictType")]
    public string ConflictType { get; set; } = string.Empty; // VersionMismatch, Incompatible, Circular

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// represents installation staging information
/// </summary>
public class InstallationStage
{
    [JsonPropertyName("stageId")]
    public string StageId { get; set; } = string.Empty;

    [JsonPropertyName("tempPath")]
    public string TempPath { get; set; } = string.Empty;

    [JsonPropertyName("targetPath")]
    public string TargetPath { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public List<StagedFile> Files { get; set; } = new();

    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public InstallationStageStatus Status { get; set; } = InstallationStageStatus.Pending;

    [JsonPropertyName("validationResults")]
    public List<ValidationResult> ValidationResults { get; set; } = new();
}

/// <summary>
/// represents a staged file
/// </summary>
public class StagedFile
{
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;

    [JsonPropertyName("targetPath")]
    public string TargetPath { get; set; } = string.Empty;

    [JsonPropertyName("checksum")]
    public string? Checksum { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("isExecutable")]
    public bool IsExecutable { get; set; } = false;
}

/// <summary>
/// represents installation stage status
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InstallationStageStatus
{
    Pending,
    Staging,
    Validating,
    Testing,
    Ready,
    Failed,
    RolledBack
}

/// <summary>
/// represents validation result
/// </summary>
public class ValidationResult
{
    [JsonPropertyName("type")]
    public ValidationType Type { get; set; }

    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// represents validation types
/// </summary>
public enum ValidationType
{
    Checksum,
    Signature,
    Dependency,
    FileIntegrity,
    Permissions,
    GameCompatibility,
    BepInExCompatibility
}

/// <summary>
/// represents user permissions and elevation status
/// </summary>
public class UserPermissions
{
    [JsonPropertyName("isElevated")]
    public bool IsElevated { get; set; }

    [JsonPropertyName("canWriteToGameDirectory")]
    public bool CanWriteToGameDirectory { get; set; }

    [JsonPropertyName("canCreateSystemFiles")]
    public bool CanCreateSystemFiles { get; set; }

    [JsonPropertyName("canModifyRegistry")]
    public bool CanModifyRegistry { get; set; }

    [JsonPropertyName("requiresElevation")]
    public bool RequiresElevation { get; set; }

    [JsonPropertyName("elevationReason")]
    public string? ElevationReason { get; set; }
}

/// <summary>
/// represents enhanced backup information with restore points
/// </summary>
public class RestorePoint
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("backupPath")]
    public string BackupPath { get; set; } = string.Empty;

    [JsonPropertyName("gameInstall")]
    public GameInstallation GameInstall { get; set; } = new();

    [JsonPropertyName("mods")]
    public List<ModInfo> Mods { get; set; } = new();

    [JsonPropertyName("bepInExVersion")]
    public string? BepInExVersion { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("isAutomatic")]
    public bool IsAutomatic { get; set; } = false;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// represents backup manifest information
/// </summary>
public class BackupManifest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; }

    [JsonPropertyName("gameInstall")]
    public GameInstallation GameInstall { get; set; } = new();

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("mods")]
    public List<ModInfo> Mods { get; set; } = new();

    [JsonPropertyName("bepInExVersion")]
    public string? BepInExVersion { get; set; }
}

/// <summary>
/// represents automatic update settings for a mod
/// </summary>
public class ModUpdateSettings
{
    [JsonPropertyName("modId")]
    public string ModId { get; set; } = string.Empty;

    [JsonPropertyName("autoUpdateEnabled")]
    public bool AutoUpdateEnabled { get; set; } = false;

    [JsonPropertyName("updateChannel")]
    public string UpdateChannel { get; set; } = "stable"; // stable, beta, alpha

    [JsonPropertyName("updateFrequency")]
    public TimeSpan UpdateFrequency { get; set; } = TimeSpan.FromDays(1);

    [JsonPropertyName("lastUpdateCheck")]
    public DateTime? LastUpdateCheck { get; set; }

    [JsonPropertyName("notifyOnUpdates")]
    public bool NotifyOnUpdates { get; set; } = true;

    [JsonPropertyName("backupBeforeUpdate")]
    public bool BackupBeforeUpdate { get; set; } = true;

    [JsonPropertyName("preserveConfigs")]
    public bool PreserveConfigs { get; set; } = true;

    [JsonPropertyName("pendingBackupPath")]
    public string? PendingBackupPath { get; set; }
}

/// <summary>
/// represents an available mod update
/// </summary>
public class ModUpdate
{
    [JsonPropertyName("modId")]
    public string ModId { get; set; } = string.Empty;

    [JsonPropertyName("currentVersion")]
    public string CurrentVersion { get; set; } = string.Empty;

    [JsonPropertyName("availableVersion")]
    public string AvailableVersion { get; set; } = string.Empty;

    [JsonPropertyName("updateType")]
    public UpdateType UpdateType { get; set; } = UpdateType.Minor;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("releaseDate")]
    public DateTime ReleaseDate { get; set; }

    [JsonPropertyName("isPrerelease")]
    public bool IsPrerelease { get; set; } = false;

    [JsonPropertyName("breakingChanges")]
    public List<string> BreakingChanges { get; set; } = new();

    [JsonPropertyName("requiresGameVersion")]
    public string? RequiresGameVersion { get; set; }

    [JsonPropertyName("requiresBepInExVersion")]
    public string? RequiresBepInExVersion { get; set; }
}

/// <summary>
/// represents types of updates
/// </summary>
public enum UpdateType
{
    Patch,      // 1.0.0 -> 1.0.1
    Minor,      // 1.0.0 -> 1.1.0
    Major,      // 1.0.0 -> 2.0.0
    Breaking    // Contains breaking changes
}

/// <summary>
/// represents mod compatibility status
/// </summary>
public class ModCompatibility
{
    [JsonPropertyName("modId")]
    public string ModId { get; set; } = string.Empty;

    [JsonPropertyName("gameVersion")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("bepInExVersion")]
    public string BepInExVersion { get; set; } = string.Empty;

    [JsonPropertyName("compatibilityStatus")]
    public CompatibilityStatus Status { get; set; } = CompatibilityStatus.Unknown;

    [JsonPropertyName("lastTested")]
    public DateTime? LastTested { get; set; }

    [JsonPropertyName("testedBy")]
    public string? TestedBy { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("issues")]
    public List<string> Issues { get; set; } = new();

    [JsonPropertyName("recommendedVersions")]
    public List<string> RecommendedVersions { get; set; } = new();
}

/// <summary>
/// represents compatibility status levels
/// </summary>
public enum CompatibilityStatus
{
    Unknown,
    Compatible,
    CompatibleWithIssues,
    Incompatible,
    Untested,
    Deprecated
}

/// <summary>
/// represents a mod configuration file
/// </summary>
public class ModConfig
{
    [JsonPropertyName("modId")]
    public string ModId { get; set; } = string.Empty;

    [JsonPropertyName("configPath")]
    public string ConfigPath { get; set; } = string.Empty;

    [JsonPropertyName("configType")]
    public ConfigType ConfigType { get; set; } = ConfigType.Unknown;

    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; set; }

    [JsonPropertyName("isUserModified")]
    public bool IsUserModified { get; set; } = false;

    [JsonPropertyName("backupPath")]
    public string? BackupPath { get; set; }
}

/// <summary>
/// represents configuration file types
/// </summary>
public enum ConfigType
{
    Unknown,
    Json,
    Yaml,
    Ini,
    Xml,
    Toml,
    Properties
}

/// <summary>
/// represents a mod conflict
/// </summary>
public class ModConflict
{
    [JsonPropertyName("conflictId")]
    public string ConflictId { get; set; } = string.Empty;

    [JsonPropertyName("conflictType")]
    public ConflictType ConflictType { get; set; } = ConflictType.FileConflict;

    [JsonPropertyName("conflictingMods")]
    public List<string> ConflictingMods { get; set; } = new();

    [JsonPropertyName("conflictingFiles")]
    public List<string> ConflictingFiles { get; set; } = new();

    [JsonPropertyName("severity")]
    public ConflictSeverity Severity { get; set; } = ConflictSeverity.Warning;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("resolution")]
    public ConflictResolution? Resolution { get; set; }

    [JsonPropertyName("detectedAt")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// represents types of conflicts
/// </summary>
public enum ConflictType
{
    FileConflict,           // Same file names
    DependencyConflict,     // Conflicting dependencies
    VersionConflict,        // Version incompatibilities
    LoadOrderConflict,      // Load order issues
    ConfigConflict,         // Configuration conflicts
    RegistryConflict        // Registry key conflicts
}

/// <summary>
/// represents conflict severity levels
/// </summary>
public enum ConflictSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// represents conflict resolution options
/// </summary>
public class ConflictResolution
{
    [JsonPropertyName("resolutionType")]
    public ResolutionType ResolutionType { get; set; } = ResolutionType.Manual;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("actions")]
    public List<ResolutionAction> Actions { get; set; } = new();

    [JsonPropertyName("canAutoResolve")]
    public bool CanAutoResolve { get; set; } = false;
}

/// <summary>
/// represents resolution types
/// </summary>
public enum ResolutionType
{
    Manual,
    Automatic,
    UserChoice,
    Skip
}

/// <summary>
/// represents a resolution action
/// </summary>
public class ResolutionAction
{
    [JsonPropertyName("actionType")]
    public ActionType ActionType { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// represents detailed BepInEx version information
/// </summary>
public class BepInExVersionInfo
{
    [JsonPropertyName("fileVersion")]
    public string? FileVersion { get; set; }

    [JsonPropertyName("productVersion")]
    public string? ProductVersion { get; set; }

    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("loaderVersion")]
    public string? LoaderVersion { get; set; }

    [JsonPropertyName("coreVersion")]
    public string? CoreVersion { get; set; }

    [JsonPropertyName("loaderPath")]
    public string? LoaderPath { get; set; }

    [JsonPropertyName("corePath")]
    public string? CorePath { get; set; }
}

/// <summary>
/// represents action types for conflict resolution
/// </summary>
public enum ActionType
{
    RenameFile,
    MoveFile,
    DeleteFile,
    ReplaceFile,
    InstallDependency,
    UpdateMod,
    DisableMod,
    ChangeLoadOrder,
    MergeConfig,
    SkipInstallation
}

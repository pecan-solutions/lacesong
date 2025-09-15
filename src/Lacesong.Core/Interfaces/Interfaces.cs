using Lacesong.Core.Models;

namespace Lacesong.Core.Interfaces;

/// <summary>
/// interface for game detection and installation management
/// </summary>
public interface IGameDetector
{
    /// <summary>
    /// detects game installation at the specified path or automatically
    /// </summary>
    /// <param name="pathHint">optional path hint for manual detection</param>
    /// <returns>detected game installation or null if not found</returns>
    Task<GameInstallation?> DetectGameInstall(string? pathHint = null);

    /// <summary>
    /// validates if a game installation is valid and complete
    /// </summary>
    /// <param name="gameInstall">game installation to validate</param>
    /// <returns>true if valid, false otherwise</returns>
    bool ValidateGameInstall(GameInstallation gameInstall);

    /// <summary>
    /// gets all supported games from registry
    /// </summary>
    /// <returns>list of supported game configurations</returns>
    Task<List<GameInstallation>> GetSupportedGames();
}

/// <summary>
/// interface for bepinex installation and management
/// </summary>
public interface IBepInExManager
{
    /// <summary>
    /// installs bepinex to the specified game installation
    /// </summary>
    /// <param name="gameInstall">target game installation</param>
    /// <param name="options">installation options</param>
    /// <returns>operation result</returns>
    Task<OperationResult> InstallBepInEx(GameInstallation gameInstall, BepInExInstallOptions options);

    /// <summary>
    /// checks if bepinex is already installed
    /// </summary>
    /// <param name="gameInstall">game installation to check</param>
    /// <returns>true if installed, false otherwise</returns>
    bool IsBepInExInstalled(GameInstallation gameInstall);

    /// <summary>
    /// gets the installed bepinex version
    /// </summary>
    /// <param name="gameInstall">game installation to check</param>
    /// <returns>version string or null if not installed</returns>
    string? GetInstalledBepInExVersion(GameInstallation gameInstall);

    /// <summary>
    /// uninstalls bepinex from the game installation
    /// </summary>
    /// <param name="gameInstall">game installation to uninstall from</param>
    /// <returns>operation result</returns>
    Task<OperationResult> UninstallBepInEx(GameInstallation gameInstall);
}

/// <summary>
/// interface for mod installation and management
/// </summary>
public interface IModManager
{
    /// <summary>
    /// installs a mod from a zip file or url
    /// </summary>
    /// <param name="source">zip file path or download url</param>
    /// <param name="gameInstall">target game installation</param>
    /// <returns>operation result with mod info</returns>
    Task<OperationResult> InstallModFromZip(string source, GameInstallation gameInstall);

    /// <summary>
    /// uninstalls a mod by its id
    /// </summary>
    /// <param name="modId">mod id to uninstall</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> UninstallMod(string modId, GameInstallation gameInstall);

    /// <summary>
    /// enables a mod by its id
    /// </summary>
    /// <param name="modId">mod id to enable</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> EnableMod(string modId, GameInstallation gameInstall);

    /// <summary>
    /// disables a mod by its id
    /// </summary>
    /// <param name="modId">mod id to disable</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> DisableMod(string modId, GameInstallation gameInstall);

    /// <summary>
    /// gets all installed mods for a game installation
    /// </summary>
    /// <param name="gameInstall">game installation</param>
    /// <returns>list of installed mods</returns>
    Task<List<ModInfo>> GetInstalledMods(GameInstallation gameInstall);

    /// <summary>
    /// gets mod info by id
    /// </summary>
    /// <param name="modId">mod id</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>mod info or null if not found</returns>
    Task<ModInfo?> GetModInfo(string modId, GameInstallation gameInstall);
}

/// <summary>
/// interface for backup and restore operations
/// </summary>
public interface IBackupManager
{
    /// <summary>
    /// creates a backup of the current mod configuration
    /// </summary>
    /// <param name="gameInstall">game installation to backup</param>
    /// <param name="backupName">name for the backup</param>
    /// <returns>operation result with backup path</returns>
    Task<OperationResult> CreateBackup(GameInstallation gameInstall, string backupName);

    /// <summary>
    /// restores a backup
    /// </summary>
    /// <param name="backupPath">path to the backup file</param>
    /// <param name="gameInstall">target game installation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> RestoreBackup(string backupPath, GameInstallation gameInstall);

    /// <summary>
    /// lists all available backups
    /// </summary>
    /// <param name="gameInstall">game installation</param>
    /// <returns>list of backup info</returns>
    Task<List<BackupInfo>> ListBackups(GameInstallation gameInstall);

    /// <summary>
    /// deletes a backup
    /// </summary>
    /// <param name="backupPath">path to the backup file</param>
    /// <returns>operation result</returns>
    Task<OperationResult> DeleteBackup(string backupPath);
}


/// <summary>
/// interface for mod index management and discovery
/// </summary>
public interface IModIndexService
{
    /// <summary>
    /// fetches the mod index from the specified repository
    /// </summary>
    /// <param name="repositoryUrl">repository url to fetch from</param>
    /// <returns>mod index or null if failed</returns>
    Task<ModIndex?> FetchModIndex(string repositoryUrl);

    /// <summary>
    /// searches for mods using the specified criteria
    /// </summary>
    /// <param name="criteria">search criteria</param>
    /// <returns>search results</returns>
    Task<ModSearchResults> SearchMods(ModSearchCriteria criteria);

    /// <summary>
    /// gets a specific mod by id
    /// </summary>
    /// <param name="modId">mod id</param>
    /// <returns>mod index entry or null if not found</returns>
    Task<ModIndexEntry?> GetMod(string modId);

    /// <summary>
    /// gets all available categories
    /// </summary>
    /// <returns>list of categories</returns>
    Task<List<string>> GetCategories();

    /// <summary>
    /// refreshes the local mod index cache
    /// </summary>
    /// <returns>operation result</returns>
    Task<OperationResult> RefreshIndex();

    /// <summary>
    /// adds a custom repository to the index
    /// </summary>
    /// <param name="repository">repository configuration</param>
    /// <returns>operation result</returns>
    Task<OperationResult> AddRepository(ModRepository repository);

    /// <summary>
    /// removes a repository from the index
    /// </summary>
    /// <param name="repositoryId">repository id to remove</param>
    /// <returns>operation result</returns>
    Task<OperationResult> RemoveRepository(string repositoryId);

    /// <summary>
    /// gets all configured repositories
    /// </summary>
    /// <returns>list of repositories</returns>
    Task<List<ModRepository>> GetRepositories();
}

/// <summary>
/// interface for github releases integration
/// </summary>
public interface IGitHubReleasesService
{
    /// <summary>
    /// scans a github repository for mod releases
    /// </summary>
    /// <param name="owner">repository owner</param>
    /// <param name="repo">repository name</param>
    /// <returns>list of mod index entries</returns>
    Task<List<ModIndexEntry>> ScanRepositoryForMods(string owner, string repo);

    /// <summary>
    /// gets release assets for a specific release
    /// </summary>
    /// <param name="owner">repository owner</param>
    /// <param name="repo">repository name</param>
    /// <param name="releaseId">release id</param>
    /// <returns>list of release assets</returns>
    Task<List<GitHubReleaseAsset>> GetReleaseAssets(string owner, string repo, string releaseId);

    /// <summary>
    /// validates if a repository contains mod releases
    /// </summary>
    /// <param name="owner">repository owner</param>
    /// <param name="repo">repository name</param>
    /// <returns>true if contains mod releases</returns>
    Task<bool> IsModRepository(string owner, string repo);
}

/// <summary>
/// represents a github release asset
/// </summary>
public class GitHubReleaseAsset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long Size { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// interface for dependency resolution and compatibility checking
/// </summary>
public interface IDependencyResolver
{
    /// <summary>
    /// resolves dependencies for a mod and checks compatibility
    /// </summary>
    /// <param name="modInfo">mod to resolve dependencies for</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>dependency resolution result</returns>
    Task<DependencyResolution> ResolveDependencies(ModInfo modInfo, GameInstallation gameInstall);

    /// <summary>
    /// installs missing dependencies from the resolution
    /// </summary>
    /// <param name="resolution">dependency resolution result</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> InstallMissingDependencies(DependencyResolution resolution, GameInstallation gameInstall);
}

/// <summary>
/// interface for signature and checksum verification
/// </summary>
public interface IVerificationService
{
    /// <summary>
    /// verifies file signature
    /// </summary>
    /// <param name="filePath">path to file</param>
    /// <param name="signature">expected signature</param>
    /// <returns>validation result</returns>
    Task<ValidationResult> VerifySignature(string filePath, FileSignature signature);

    /// <summary>
    /// verifies file checksum
    /// </summary>
    /// <param name="filePath">path to file</param>
    /// <param name="expectedChecksum">expected checksum</param>
    /// <param name="algorithm">checksum algorithm</param>
    /// <returns>validation result</returns>
    Task<ValidationResult> VerifyChecksum(string filePath, string expectedChecksum, string algorithm = "SHA256");

    /// <summary>
    /// calculates file checksum
    /// </summary>
    /// <param name="filePath">path to file</param>
    /// <param name="algorithm">checksum algorithm</param>
    /// <returns>calculated checksum</returns>
    Task<string> CalculateChecksum(string filePath, string algorithm = "SHA256");

    /// <summary>
    /// verifies file integrity
    /// </summary>
    /// <param name="filePath">path to file</param>
    /// <returns>validation result</returns>
    Task<ValidationResult> VerifyFileIntegrity(string filePath);

    /// <summary>
    /// verifies file permissions
    /// </summary>
    /// <param name="filePath">path to file</param>
    /// <param name="requireWrite">whether write permission is required</param>
    /// <returns>validation result</returns>
    Task<ValidationResult> VerifyPermissions(string filePath, bool requireWrite = false);
}

/// <summary>
/// interface for safe installation staging
/// </summary>
public interface IInstallationStager
{
    /// <summary>
    /// creates a staging area for installation
    /// </summary>
    /// <param name="targetPath">target installation path</param>
    /// <returns>installation stage</returns>
    Task<InstallationStage> CreateStage(string targetPath);

    /// <summary>
    /// stages files for installation
    /// </summary>
    /// <param name="stage">installation stage</param>
    /// <param name="sourceFiles">files to stage</param>
    /// <returns>operation result</returns>
    Task<OperationResult> StageFiles(InstallationStage stage, List<string> sourceFiles);

    /// <summary>
    /// validates staged installation
    /// </summary>
    /// <param name="stage">installation stage</param>
    /// <returns>validation results</returns>
    Task<List<ValidationResult>> ValidateStage(InstallationStage stage);

    /// <summary>
    /// commits staged installation to final location
    /// </summary>
    /// <param name="stage">installation stage</param>
    /// <returns>operation result</returns>
    Task<OperationResult> CommitStage(InstallationStage stage);

    /// <summary>
    /// rolls back staged installation
    /// </summary>
    /// <param name="stage">installation stage</param>
    /// <returns>operation result</returns>
    Task<OperationResult> RollbackStage(InstallationStage stage);

    /// <summary>
    /// cleans up staging area
    /// </summary>
    /// <param name="stage">installation stage</param>
    /// <returns>operation result</returns>
    Task<OperationResult> CleanupStage(InstallationStage stage);
}

/// <summary>
/// interface for user permissions and elevation management
/// </summary>
public interface IPermissionsService
{
    /// <summary>
    /// checks current user permissions
    /// </summary>
    /// <param name="gameInstall">game installation to check permissions for</param>
    /// <returns>user permissions status</returns>
    Task<UserPermissions> CheckPermissions(GameInstallation gameInstall);

    /// <summary>
    /// requests elevation if needed
    /// </summary>
    /// <param name="reason">reason for elevation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> RequestElevation(string reason);

    /// <summary>
    /// checks if elevation is required for operation
    /// </summary>
    /// <param name="operation">operation to check</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>true if elevation required</returns>
    Task<bool> RequiresElevation(string operation, GameInstallation gameInstall);
}

/// <summary>
/// interface for automatic mod updates
/// </summary>
public interface IModUpdateService
{
    /// <summary>
    /// checks for available updates for installed mods
    /// </summary>
    /// <param name="gameInstall">game installation</param>
    /// <param name="modIds">specific mod ids to check, null for all</param>
    /// <returns>list of available updates</returns>
    Task<List<ModUpdate>> CheckForUpdates(GameInstallation gameInstall, List<string>? modIds = null);

    /// <summary>
    /// installs a mod update
    /// </summary>
    /// <param name="update">update to install</param>
    /// <param name="gameInstall">game installation</param>
    /// <param name="options">update options</param>
    /// <returns>operation result</returns>
    Task<OperationResult> InstallUpdate(ModUpdate update, GameInstallation gameInstall, ModUpdateSettings? options = null);

    /// <summary>
    /// gets update settings for a mod
    /// </summary>
    /// <param name="modId">mod id</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>update settings</returns>
    Task<ModUpdateSettings> GetUpdateSettings(string modId, GameInstallation gameInstall);

    /// <summary>
    /// sets update settings for a mod
    /// </summary>
    /// <param name="settings">update settings</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> SetUpdateSettings(ModUpdateSettings settings, GameInstallation gameInstall);

    /// <summary>
    /// schedules automatic update checks
    /// </summary>
    /// <param name="gameInstall">game installation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> ScheduleUpdateChecks(GameInstallation gameInstall);

    /// <summary>
    /// cancels scheduled update checks
    /// </summary>
    /// <returns>operation result</returns>
    Task<OperationResult> CancelUpdateChecks();
}

/// <summary>
/// interface for mod conflict detection and resolution
/// </summary>
public interface IConflictDetectionService
{
    /// <summary>
    /// detects conflicts between mods
    /// </summary>
    /// <param name="gameInstall">game installation</param>
    /// <param name="modToInstall">mod being installed (optional)</param>
    /// <returns>list of detected conflicts</returns>
    Task<List<ModConflict>> DetectConflicts(GameInstallation gameInstall, ModInfo? modToInstall = null);

    /// <summary>
    /// resolves a conflict automatically if possible
    /// </summary>
    /// <param name="conflict">conflict to resolve</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> ResolveConflict(ModConflict conflict, GameInstallation gameInstall);

    /// <summary>
    /// gets available resolution options for a conflict
    /// </summary>
    /// <param name="conflict">conflict to analyze</param>
    /// <returns>list of resolution options</returns>
    Task<List<ConflictResolution>> GetResolutionOptions(ModConflict conflict);

    /// <summary>
    /// validates that a resolution can be applied
    /// </summary>
    /// <param name="conflict">conflict</param>
    /// <param name="resolution">proposed resolution</param>
    /// <returns>validation result</returns>
    Task<ValidationResult> ValidateResolution(ModConflict conflict, ConflictResolution resolution);
}

/// <summary>
/// interface for mod configuration management
/// </summary>
public interface IModConfigService
{
    /// <summary>
    /// gets all configuration files for a mod
    /// </summary>
    /// <param name="modId">mod id</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>list of config files</returns>
    Task<List<ModConfig>> GetModConfigs(string modId, GameInstallation gameInstall);

    /// <summary>
    /// backs up mod configuration files
    /// </summary>
    /// <param name="modId">mod id</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> BackupModConfigs(string modId, GameInstallation gameInstall);

    /// <summary>
    /// restores mod configuration files from backup
    /// </summary>
    /// <param name="modId">mod id</param>
    /// <param name="gameInstall">game installation</param>
    /// <param name="backupPath">backup path</param>
    /// <returns>operation result</returns>
    Task<OperationResult> RestoreModConfigs(string modId, GameInstallation gameInstall, string? backupPath = null);

    /// <summary>
    /// merges configuration files during mod update
    /// </summary>
    /// <param name="modId">mod id</param>
    /// <param name="oldConfigs">old configuration files</param>
    /// <param name="newConfigs">new configuration files</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>operation result</returns>
    Task<OperationResult> MergeConfigs(string modId, List<ModConfig> oldConfigs, List<ModConfig> newConfigs, GameInstallation gameInstall);

    /// <summary>
    /// detects if a config file has been user-modified
    /// </summary>
    /// <param name="configPath">config file path</param>
    /// <param name="originalChecksum">original file checksum</param>
    /// <returns>true if modified</returns>
    Task<bool> IsConfigModified(string configPath, string originalChecksum);
}

/// <summary>
/// interface for mod compatibility checking
/// </summary>
public interface ICompatibilityService
{
    /// <summary>
    /// checks compatibility of a mod with current game version
    /// </summary>
    /// <param name="modId">mod id</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>compatibility status</returns>
    Task<ModCompatibility> CheckCompatibility(string modId, GameInstallation gameInstall);

    /// <summary>
    /// checks compatibility of multiple mods
    /// </summary>
    /// <param name="modIds">list of mod ids</param>
    /// <param name="gameInstall">game installation</param>
    /// <returns>list of compatibility statuses</returns>
    Task<List<ModCompatibility>> CheckCompatibility(List<string> modIds, GameInstallation gameInstall);

    /// <summary>
    /// gets recommended mod versions for current game version
    /// </summary>
    /// <param name="modId">mod id</param>
    /// <param name="gameVersion">game version</param>
    /// <returns>list of recommended versions</returns>
    Task<List<string>> GetRecommendedVersions(string modId, string gameVersion);

    /// <summary>
    /// reports compatibility issues for a mod
    /// </summary>
    /// <param name="compatibility">compatibility report</param>
    /// <returns>operation result</returns>
    Task<OperationResult> ReportCompatibilityIssue(ModCompatibility compatibility);
}

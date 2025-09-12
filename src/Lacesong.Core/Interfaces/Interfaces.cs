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

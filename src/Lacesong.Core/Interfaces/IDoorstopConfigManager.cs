using Lacesong.Core.Models;

namespace Lacesong.Core.Interfaces;

/// <summary>
/// interface for managing doorstop configuration on windows
/// </summary>
public interface IDoorstopConfigManager
{
    /// <summary>
    /// checks if doorstop is installed (doorstop_config.ini exists)
    /// </summary>
    bool IsDoorstopInstalled(GameInstallation gameInstall);

    /// <summary>
    /// checks if doorstop is currently enabled in the config
    /// </summary>
    Task<bool> IsDoorstopEnabled(GameInstallation gameInstall);

    /// <summary>
    /// sets the doorstop enabled state in the config file
    /// </summary>
    Task<OperationResult> SetDoorstopEnabled(GameInstallation gameInstall, bool enabled);

    /// <summary>
    /// disables doorstop by setting enabled=false in doorstop_config.ini
    /// </summary>
    Task<OperationResult> DisableDoorstop(GameInstallation gameInstall);

    /// <summary>
    /// enables doorstop by setting enabled=true in doorstop_config.ini
    /// </summary>
    Task<OperationResult> EnableDoorstop(GameInstallation gameInstall);
}

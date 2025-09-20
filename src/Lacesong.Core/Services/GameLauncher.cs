using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Diagnostics;
using System.IO;

namespace Lacesong.Core.Services;

/// <summary>
/// handles launching the game in vanilla or modded mode
/// </summary>
public class GameLauncher : IGameLauncher
{
    private readonly IBepInExManager _bepInExManager;
    private readonly IModManager _modManager;

    public GameLauncher(IBepInExManager bepInExManager, IModManager modManager)
    {
        _bepInExManager = bepInExManager;
        _modManager = modManager;
    }

    public Task<OperationResult> LaunchModded(GameInstallation gameInstall)
    {
        // ensure mods directory exists so any first-run setup is complete
        ModManager.EnsureModsDirectory(gameInstall); // static helper

        if (!_bepInExManager.IsBepInExInstalled(gameInstall))
        {
            return Task.FromResult(OperationResult.ErrorResult("BepInEx is not installed", "modded launch failed"));
        }

        return StartGame(gameInstall);
    }

    public async Task<OperationResult> LaunchVanilla(GameInstallation gameInstall)
    {
        // ensure mods directory exists even for vanilla launches
        ModManager.EnsureModsDirectory(gameInstall);
        var bepinexPath = Path.Combine(gameInstall.InstallPath, "BepInEx");
        var tempDisabledPath = bepinexPath + "_disabled";
        try
        {
            // temporarily disable BepInEx folder
            if (Directory.Exists(bepinexPath))
            {
                if (Directory.Exists(tempDisabledPath))
                    Directory.Delete(tempDisabledPath, true);
                Directory.Move(bepinexPath, tempDisabledPath);
            }

            var result = await StartGame(gameInstall);

            return result;
        }
        finally
        {
            // restore folder after exit
            RestoreBepInEx(bepinexPath, tempDisabledPath);
        }
    }

    private static Task<OperationResult> StartGame(GameInstallation gameInstall)
    {
        try
        {
            var exePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
            if (!File.Exists(exePath))
            {
                return Task.FromResult(OperationResult.ErrorResult("game executable not found", "launch failed"));
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = gameInstall.InstallPath,
                UseShellExecute = true
            };
            Process.Start(psi);
            return Task.FromResult(OperationResult.SuccessResult("game launched"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.ErrorResult(ex.Message, "launch failed"));
        }
    }

    private static void RestoreBepInEx(string originalPath, string disabledPath)
    {
        try
        {
            if (Directory.Exists(disabledPath) && !Directory.Exists(originalPath))
            {
                Directory.Move(disabledPath, originalPath);
            }
        }
        catch { }
    }
}

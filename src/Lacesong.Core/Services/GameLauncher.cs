using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Runtime.InteropServices;

namespace Lacesong.Core.Services;

/// <summary>
/// handles launching the game in vanilla or modded mode
/// </summary>
public class GameLauncher : IGameLauncher
{
    private readonly IBepInExManager _bepInExManager;
    private readonly IModManager _modManager;
    // map install path to list of processes we spawned so we can stop them later
    private readonly ConcurrentDictionary<string, List<Process>> _runningProcesses = new();

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

        return StartGameInternal(gameInstall, isVanilla:false);
    }

    public async Task<OperationResult> LaunchVanilla(GameInstallation gameInstall)
    {
        // ensure mods directory exists even for vanilla launches
        ModManager.EnsureModsDirectory(gameInstall);
        var bepinexPath = Path.Combine(gameInstall.InstallPath, "BepInEx");
        var pluginsPath = Path.Combine(bepinexPath, "plugins");

        // when launching vanilla we only need to hide plugins so that bepinex loads with no mods.
        // moving the entire bepinex folder caused unnecessary io and increased risk of corrupting the install.
        // instead, we temporarily rename the plugins folder (or symlink target) and restore after we finish starting the game.
        var tempDisabledPath = pluginsPath + "_disabled";
        try
        {
            // temporarily disable plugins folder (symlinks) to ensure a pure vanilla launch
            if (Directory.Exists(pluginsPath))
            {
                if (Directory.Exists(tempDisabledPath))
                    Directory.Delete(tempDisabledPath, true);

                // use move to keep operation fast even for large plugin sets. this is effectively O(1) as it just changes directory entry.
                Directory.Move(pluginsPath, tempDisabledPath);
            }

            var result = await StartGameInternal(gameInstall, isVanilla:true);

            return result;
        }
        finally
        {
            // restore plugins folder immediately after launch attempt – this mirrors previous behaviour where bepinex was restored right away.
            // note: we don't wait for game process exit to remain non-blocking. this keeps previous semantics while avoiding heavy directory moves.
            RestorePluginsFolder(pluginsPath, tempDisabledPath);
        }
    }

    private Task<OperationResult> StartGameInternal(GameInstallation gameInstall, bool isVanilla)
    {
        try
        {
            var procList = new List<Process>();

            // on mac/linux run prelaunch script if it exists
            if (!OperatingSystem.IsWindows())
            {
                var scriptPath = Path.Combine(gameInstall.InstallPath, "run_bepinex.sh");
                if (File.Exists(scriptPath))
                {
                    var psiScript = new ProcessStartInfo
                    {
                        FileName = scriptPath,
                        WorkingDirectory = gameInstall.InstallPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    var scriptProc = Process.Start(psiScript);
                    if (scriptProc != null) procList.Add(scriptProc);
                }
            }

            var exePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
            if (!File.Exists(exePath))
            {
                return Task.FromResult(OperationResult.ErrorResult("game executable not found", "launch failed"));
            }

            var psiGame = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = gameInstall.InstallPath,
                UseShellExecute = true
            };
            var gameProc = Process.Start(psiGame);
            if (gameProc != null) procList.Add(gameProc);

            if (procList.Count > 0)
            {
                _runningProcesses[gameInstall.InstallPath] = procList;
            }

            return Task.FromResult(OperationResult.SuccessResult("game launched"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.ErrorResult(ex.Message, "launch failed"));
        }
    }

    private static void RestorePluginsFolder(string originalPath, string disabledPath)
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

    public Task<OperationResult> Stop(GameInstallation gameInstall)
    {
        if (!_runningProcesses.TryRemove(gameInstall.InstallPath, out var procs) || procs.Count == 0)
        {
            return Task.FromResult(OperationResult.ErrorResult("game not running", "stop failed"));
        }

        foreach (var p in procs)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(true);
                }
            }
            catch { }
        }

        return Task.FromResult(OperationResult.SuccessResult("game stopped"));
    }

    public bool IsRunning(GameInstallation gameInstall) => _runningProcesses.ContainsKey(gameInstall.InstallPath);
}

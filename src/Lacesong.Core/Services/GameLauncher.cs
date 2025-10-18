using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using System.Threading;

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

            // on non-windows, check for run_bepinex.sh script first
            if (!OperatingSystem.IsWindows())
            {
                var scriptPath = Path.Combine(gameInstall.InstallPath, "run_bepinex.sh");
                if (File.Exists(scriptPath))
                {
                    // only run script for modded launches (when isVanilla is false)
                    // or when policy allows vanilla launches with script
                    if (!isVanilla)
                    {
                        var psiScript = new ProcessStartInfo
                        {
                            FileName = scriptPath,
                            WorkingDirectory = gameInstall.InstallPath,
                            UseShellExecute = true
                        };
                        var scriptProc = Process.Start(psiScript);
                        if (scriptProc != null) procList.Add(scriptProc);
                        
                        // script handles launching the game, so we're done
                        if (procList.Count > 0)
                        {
                            _runningProcesses[gameInstall.InstallPath] = procList;
                        }
                        return Task.FromResult(OperationResult.SuccessResult("game launched via script"));
                    }
                }
            }

            // fallback to direct executable launch (windows or when no script exists)
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

    public async Task<OperationResult> Stop(GameInstallation gameInstall)
    {
        if (!_runningProcesses.TryRemove(gameInstall.InstallPath, out var procs) || procs.Count == 0)
        {
            return OperationResult.ErrorResult("game not running", "stop failed");
        }

        var failedProcesses = new List<string>();
        var timeoutMs = 5000; // 5 second timeout for graceful shutdown

        foreach (var p in procs)
        {
            try
            {
                if (p.HasExited)
                    continue;

                bool gracefulShutdownSucceeded = false;

                // try graceful shutdown first (close main window)
                if (OperatingSystem.IsWindows() && !p.MainWindowHandle.Equals(IntPtr.Zero))
                {
                    try
                    {
                        gracefulShutdownSucceeded = p.CloseMainWindow();
                    }
                    catch
                    {
                        // fall through to kill if close main window fails
                    }
                }

                // wait for graceful shutdown with timeout
                if (gracefulShutdownSucceeded)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(timeoutMs);
                        await p.WaitForExitAsync(cts.Token);
                        gracefulShutdownSucceeded = p.HasExited;
                    }
                    catch
                    {
                        gracefulShutdownSucceeded = false;
                    }
                }

                // if graceful shutdown failed or timed out, force kill
                if (!gracefulShutdownSucceeded)
                {
                    try
                    {
                        p.Kill(true);
                        // give a brief moment for kill to take effect
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        failedProcesses.Add($"{p.ProcessName} (PID: {p.Id}): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                failedProcesses.Add($"{p.ProcessName} (PID: {p.Id}): {ex.Message}");
            }
        }

        // report results
        if (failedProcesses.Count > 0)
        {
            var errorMessage = $"failed to stop {failedProcesses.Count} process(es): {string.Join("; ", failedProcesses)}";
            return OperationResult.ErrorResult(errorMessage, "stop failed");
        }

        return OperationResult.SuccessResult("game stopped gracefully");
    }

    public bool IsRunning(GameInstallation gameInstall) => _runningProcesses.ContainsKey(gameInstall.InstallPath);
}

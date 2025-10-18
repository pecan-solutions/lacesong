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
        Console.WriteLine($"[DEBUG] GameLauncher.LaunchVanilla called with gameInstall: {(gameInstall != null ? $"Path: {gameInstall.InstallPath}, Executable: {gameInstall.Executable}" : "null")}");
        
        // ensure mods directory exists even for vanilla launches
        Console.WriteLine($"[DEBUG] Ensuring mods directory exists");
        ModManager.EnsureModsDirectory(gameInstall);
        
        var bepinexPath = Path.Combine(gameInstall.InstallPath, "BepInEx");
        var pluginsPath = Path.Combine(bepinexPath, "plugins");
        Console.WriteLine($"[DEBUG] BepInEx path: {bepinexPath}");
        Console.WriteLine($"[DEBUG] Plugins path: {pluginsPath}");
        Console.WriteLine($"[DEBUG] Plugins directory exists: {Directory.Exists(pluginsPath)}");

        // when launching vanilla we only need to hide plugins so that bepinex loads with no mods.
        // moving the entire bepinex folder caused unnecessary io and increased risk of corrupting the install.
        // instead, we temporarily rename the plugins folder (or symlink target) and restore after we finish starting the game.
        var tempDisabledPath = pluginsPath + "_disabled";
        Console.WriteLine($"[DEBUG] Temp disabled path: {tempDisabledPath}");
        
        try
        {
            // temporarily disable plugins folder (symlinks) to ensure a pure vanilla launch
            if (Directory.Exists(pluginsPath))
            {
                Console.WriteLine($"[DEBUG] Plugins directory exists, disabling it for vanilla launch");
                if (Directory.Exists(tempDisabledPath))
                {
                    Console.WriteLine($"[DEBUG] Temp disabled directory already exists, deleting it");
                    Directory.Delete(tempDisabledPath, true);
                }

                // use move to keep operation fast even for large plugin sets. this is effectively O(1) as it just changes directory entry.
                Console.WriteLine($"[DEBUG] Moving plugins directory to disabled location");
                Directory.Move(pluginsPath, tempDisabledPath);
                Console.WriteLine($"[DEBUG] Successfully moved plugins directory");
            }
            else
            {
                Console.WriteLine($"[DEBUG] Plugins directory does not exist, skipping disable step");
            }

            Console.WriteLine($"[DEBUG] Calling StartGameInternal with isVanilla=true");
            var result = await StartGameInternal(gameInstall, isVanilla:true);
            Console.WriteLine($"[DEBUG] StartGameInternal returned - Success: {result.Success}, Message: {result.Message}, Error: {result.Error}");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Exception in LaunchVanilla: {ex.Message}");
            Console.WriteLine($"[DEBUG] Exception stack trace: {ex.StackTrace}");
            return OperationResult.ErrorResult($"Exception in LaunchVanilla: {ex.Message}", "launch failed");
        }
        finally
        {
            // restore plugins folder immediately after launch attempt – this mirrors previous behaviour where bepinex was restored right away.
            // note: we don't wait for game process exit to remain non-blocking. this keeps previous semantics while avoiding heavy directory moves.
            Console.WriteLine($"[DEBUG] Restoring plugins folder");
            RestorePluginsFolder(pluginsPath, tempDisabledPath);
        }
    }

    private Task<OperationResult> StartGameInternal(GameInstallation gameInstall, bool isVanilla)
    {
        Console.WriteLine($"[DEBUG] StartGameInternal called with isVanilla: {isVanilla}");
        Console.WriteLine($"[DEBUG] GameInstall: {(gameInstall != null ? $"Path: {gameInstall.InstallPath}, Executable: {gameInstall.Executable}" : "null")}");
        Console.WriteLine($"[DEBUG] Operating System: {(OperatingSystem.IsWindows() ? "Windows" : "Non-Windows")}");
        
        try
        {
            var procList = new List<Process>();

            // check for run_bepinex.sh script based on executable type, not current platform
            var executablePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
            if (ExecutableTypeDetector.ShouldUseRunBepInExScript(executablePath))
            {
                Console.WriteLine($"[DEBUG] Unix executable detected, checking for run_bepinex.sh script");
                var scriptPath = Path.Combine(gameInstall.InstallPath, "run_bepinex.sh");
                Console.WriteLine($"[DEBUG] Script path: {scriptPath}");
                Console.WriteLine($"[DEBUG] Script exists: {File.Exists(scriptPath)}");
                
                if (File.Exists(scriptPath))
                {
                    // only run script for modded launches (when isVanilla is false)
                    // or when policy allows vanilla launches with script
                    if (!isVanilla)
                    {
                        Console.WriteLine($"[DEBUG] Modded launch detected, using script");
                        var psiScript = new ProcessStartInfo
                        {
                            FileName = "/usr/bin/env",
                            Arguments = $"bash \"{scriptPath}\"",
                            WorkingDirectory = gameInstall.InstallPath,
                            UseShellExecute = false
                        };
                        Console.WriteLine($"[DEBUG] Starting script with ProcessStartInfo - FileName: {psiScript.FileName}, Arguments: {psiScript.Arguments}, WorkingDirectory: {psiScript.WorkingDirectory}");
                        
                        var scriptProc = Process.Start(psiScript);
                        Console.WriteLine($"[DEBUG] Script process started: {(scriptProc != null ? $"PID: {scriptProc.Id}" : "null")}");
                        
                        if (scriptProc != null) 
                        {
                            scriptProc.EnableRaisingEvents = true;
                            procList.Add(scriptProc);
                            
                            // attach exited event to clean up tracking when script terminates
                            var scriptProcessId = scriptProc.Id;
                            var installPath = gameInstall.InstallPath;
                            EventHandler exitedHandler = (sender, e) =>
                            {
                                Console.WriteLine($"[DEBUG] Script process {scriptProcessId} exited");
                                if (_runningProcesses.TryGetValue(installPath, out var processes))
                                {
                                    lock (processes) // ensure thread-safe list operations
                                    {
                                        processes.RemoveAll(p => p.Id == scriptProcessId);
                                        if (processes.Count == 0)
                                        {
                                            _runningProcesses.TryRemove(installPath, out _);
                                        }
                                    }
                                }
                            };
                            scriptProc.Exited += exitedHandler;
                        }
                        
                        // script handles launching the game, so we're done
                        if (procList.Count > 0)
                        {
                            Console.WriteLine($"[DEBUG] Adding {procList.Count} processes to running processes");
                            _runningProcesses[gameInstall.InstallPath] = procList;
                        }
                        Console.WriteLine($"[DEBUG] Returning success for script launch");
                        return Task.FromResult(OperationResult.SuccessResult("game launched via script"));
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] Vanilla launch detected, skipping script");
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Script not found, falling back to direct executable launch");
                }
            }
            else
            {
                Console.WriteLine($"[DEBUG] Windows executable detected, skipping script check");
            }

            // fallback to direct executable launch (windows or when no script exists)
            var exePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
            Console.WriteLine($"[DEBUG] Executable path: {exePath}");
            Console.WriteLine($"[DEBUG] Executable exists: {File.Exists(exePath)}");
            
            // check for .app bundles based on executable type, not current platform
            if (!File.Exists(exePath) && ExecutableTypeDetector.IsMacOSAppBundle(exePath))
            {
                Console.WriteLine($"[DEBUG] macOS app bundle detected");
                Console.WriteLine($"[DEBUG] App bundle path: {exePath}");
                Console.WriteLine($"[DEBUG] App bundle exists: {Directory.Exists(exePath)}");
                
                if (Directory.Exists(exePath))
                {
                    exePath = ExecutableTypeDetector.GetAppBundleExecutablePath(exePath);
                    Console.WriteLine($"[DEBUG] Updated executable path for macOS: {exePath}");
                    Console.WriteLine($"[DEBUG] macOS executable exists: {File.Exists(exePath)}");
                }
            }
            else if (!File.Exists(exePath))
            {
                // check if it might be an app bundle with different naming
                var appBundlePath = Path.Combine(gameInstall.InstallPath, $"{Path.GetFileNameWithoutExtension(gameInstall.Executable)}.app");
                Console.WriteLine($"[DEBUG] Checking for alternative app bundle: {appBundlePath}");
                Console.WriteLine($"[DEBUG] Alternative app bundle exists: {Directory.Exists(appBundlePath)}");
                
                if (Directory.Exists(appBundlePath))
                {
                    exePath = ExecutableTypeDetector.GetAppBundleExecutablePath(appBundlePath);
                    Console.WriteLine($"[DEBUG] Updated executable path for alternative macOS: {exePath}");
                    Console.WriteLine($"[DEBUG] Alternative macOS executable exists: {File.Exists(exePath)}");
                }
            }
            
            if (!File.Exists(exePath))
            {
                Console.WriteLine($"[DEBUG] Executable not found, returning error");
                return Task.FromResult(OperationResult.ErrorResult("game executable not found", "launch failed"));
            }

            var psiGame = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = gameInstall.InstallPath,
                UseShellExecute = true
            };
            Console.WriteLine($"[DEBUG] Starting game with ProcessStartInfo - FileName: {psiGame.FileName}, WorkingDirectory: {psiGame.WorkingDirectory}, UseShellExecute: {psiGame.UseShellExecute}");
            
            var gameProc = Process.Start(psiGame);
            Console.WriteLine($"[DEBUG] Game process started: {(gameProc != null ? $"PID: {gameProc.Id}" : "null")}");
            
            if (gameProc != null) 
            {
                gameProc.EnableRaisingEvents = true;
                procList.Add(gameProc);
                
                // attach exited event to clean up tracking when game terminates
                var gameProcessId = gameProc.Id;
                var installPath = gameInstall.InstallPath;
                EventHandler exitedHandler = (sender, e) =>
                {
                    Console.WriteLine($"[DEBUG] Game process {gameProcessId} exited");
                    if (_runningProcesses.TryGetValue(installPath, out var processes))
                    {
                        lock (processes) // ensure thread-safe list operations
                        {
                            processes.RemoveAll(p => p.Id == gameProcessId);
                            if (processes.Count == 0)
                            {
                                _runningProcesses.TryRemove(installPath, out _);
                            }
                        }
                    }
                };
                gameProc.Exited += exitedHandler;
            }

            if (procList.Count > 0)
            {
                Console.WriteLine($"[DEBUG] Adding {procList.Count} processes to running processes");
                _runningProcesses[gameInstall.InstallPath] = procList;
            }

            Console.WriteLine($"[DEBUG] Returning success for direct executable launch");
            return Task.FromResult(OperationResult.SuccessResult("game launched"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Exception in StartGameInternal: {ex.Message}");
            Console.WriteLine($"[DEBUG] Exception stack trace: {ex.StackTrace}");
            return Task.FromResult(OperationResult.ErrorResult(ex.Message, "launch failed"));
        }
    }

    private static void RestorePluginsFolder(string originalPath, string disabledPath)
    {
        Console.WriteLine($"[DEBUG] RestorePluginsFolder called - Original: {originalPath}, Disabled: {disabledPath}");
        try
        {
            if (Directory.Exists(disabledPath) && !Directory.Exists(originalPath))
            {
                Console.WriteLine($"[DEBUG] Moving disabled folder back to original location");
                Directory.Move(disabledPath, originalPath);
                Console.WriteLine($"[DEBUG] Successfully restored plugins folder");
            }
            else
            {
                Console.WriteLine($"[DEBUG] Skipping restore - Disabled exists: {Directory.Exists(disabledPath)}, Original exists: {Directory.Exists(originalPath)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Exception in RestorePluginsFolder: {ex.Message}");
        }
    }

    public async Task<OperationResult> Stop(GameInstallation gameInstall)
    {
        if (!_runningProcesses.TryRemove(gameInstall.InstallPath, out var procs) || procs.Count == 0)
        {
            return OperationResult.ErrorResult("game not running", "stop failed");
        }

        var failedProcesses = new List<string>();
        var timeoutMs = 5000; // 5 second timeout for graceful shutdown
        var processesToWaitFor = new List<(Process process, bool needsGracefulWait, bool needsKillWait)>();

        // first pass: collect processes and attempt graceful shutdown
        lock (procs) // ensure thread-safe access to process list
        {
            foreach (var p in procs)
            {
                try
                {
                    if (p.HasExited)
                    {
                        p.Dispose();
                        continue;
                    }

                    bool gracefulShutdownSucceeded = false;

                    // try graceful shutdown first (close main window)
                    if (OperatingSystem.IsWindows() && !p.MainWindowHandle.Equals(IntPtr.Zero))
                    {
                        try
                        {
                            gracefulShutdownSucceeded = p.CloseMainWindow();
                        }
                        catch (Exception ex)
                        {
                            // fall through to kill if close main window fails
                            failedProcesses.Add($"{p.ProcessName} (PID: {p.Id}): CloseMainWindow failed - {ex.Message}");
                        }
                    }

                    // collect processes that need async waiting
                    if (gracefulShutdownSucceeded)
                    {
                        processesToWaitFor.Add((p, true, false));
                    }
                    else
                    {
                        // will need to kill and wait
                        processesToWaitFor.Add((p, false, true));
                    }
                }
                catch (Exception ex)
                {
                    failedProcesses.Add($"{p.ProcessName} (PID: {p.Id}): Unexpected error - {ex.Message}");
                    try
                    {
                        p.Dispose();
                    }
                    catch
                    {
                        // ignore disposal errors
                    }
                }
            }
        } // end lock block

        // second pass: perform async operations outside the lock
        foreach (var (process, needsGracefulWait, initialNeedsKillWait) in processesToWaitFor)
        {
            try
            {
                bool needsKillWait = initialNeedsKillWait;
                
                if (needsGracefulWait)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(timeoutMs);
                        await process.WaitForExitAsync(cts.Token);
                        
                        if (!process.HasExited)
                        {
                            // graceful shutdown timed out, need to kill
                            needsKillWait = true;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // timeout occurred, process still running
                        needsKillWait = true;
                    }
                    catch (Exception ex)
                    {
                        failedProcesses.Add($"{process.ProcessName} (PID: {process.Id}): WaitForExitAsync failed - {ex.Message}");
                        needsKillWait = true;
                    }
                }

                if (needsKillWait)
                {
                    try
                    {
                        process.Kill(true);
                        
                        // wait for kill to take effect with timeout
                        using var killCts = new CancellationTokenSource(timeoutMs);
                        await process.WaitForExitAsync(killCts.Token);
                        
                        // verify process actually exited
                        if (!process.HasExited)
                        {
                            failedProcesses.Add($"{process.ProcessName} (PID: {process.Id}): Process did not exit after Kill");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // timeout occurred, process still running
                        failedProcesses.Add($"{process.ProcessName} (PID: {process.Id}): Process did not exit after Kill (timeout)");
                    }
                    catch (Exception ex)
                    {
                        failedProcesses.Add($"{process.ProcessName} (PID: {process.Id}): Kill failed - {ex.Message}");
                    }
                }
            }
            finally
            {
                // always dispose the process object to prevent handle leaks
                try
                {
                    process.Dispose();
                }
                catch
                {
                    // ignore disposal errors
                }
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

    public bool IsRunning(GameInstallation gameInstall)
    {
        if (!_runningProcesses.TryGetValue(gameInstall.InstallPath, out var processes))
        {
            return false;
        }

        lock (processes) // ensure thread-safe access to process list
        {
            var aliveProcesses = new List<Process>();
            
            foreach (var process in processes)
            {
                try
                {
                    // refresh process info to get current state
                    process.Refresh();
                    
                    if (!process.HasExited)
                    {
                        aliveProcesses.Add(process);
                    }
                    else
                    {
                        // dispose dead process to prevent handle leaks
                        process.Dispose();
                    }
                }
                catch (Exception)
                {
                    // if we can't access the process (e.g., access denied), treat as dead
                    try
                    {
                        process.Dispose();
                    }
                    catch
                    {
                        // ignore disposal errors
                    }
                }
            }
            
            // update the process list with only alive processes
            processes.Clear();
            processes.AddRange(aliveProcesses);
            
            // if no alive processes remain, remove the entry from the dictionary
            if (processes.Count == 0)
            {
                _runningProcesses.TryRemove(gameInstall.InstallPath, out _);
                return false;
            }
            
            return true;
        }
    }
}

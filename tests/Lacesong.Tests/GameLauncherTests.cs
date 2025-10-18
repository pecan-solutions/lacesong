/// <summary>
/// unit tests for GameLauncher
/// </summary>
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Core.Services;
using Moq;
using Xunit;

namespace Lacesong.Tests;

public class GameLauncherTests
{
    private static GameInstallation CreateTempGameInstall(bool createExe = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var exeName = OperatingSystem.IsWindows() ? "dummy.exe" : "dummy";
        if (createExe)
        {
            var exePath = Path.Combine(dir, exeName);
            File.WriteAllText(exePath, string.Empty);
            if (!OperatingSystem.IsWindows())
            {
                // mark executable on unix
                try { System.Diagnostics.Process.Start("chmod", $"+x {exePath}")?.WaitForExit(); } catch { }
            }
        }
        return new GameInstallation
        {
            Name = "Dummy Game",
            InstallPath = dir,
            Executable = exeName
        };
    }

    [Fact]
    public async Task LaunchModded_ReturnsError_WhenBepInExNotInstalled()
    {
        var bepMock = new Mock<IBepInExManager>();
        bepMock.Setup(b => b.IsBepInExInstalled(It.IsAny<GameInstallation>())).Returns(false);
        var modMgr = new Mock<IModManager>().Object;
        var launcher = new GameLauncher(bepMock.Object, modMgr);
        var game = CreateTempGameInstall();

        var result = await launcher.LaunchModded(game);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task LaunchVanilla_ReturnsError_WhenExecutableMissing()
    {
        var bepMock = new Mock<IBepInExManager>();
        bepMock.Setup(b => b.IsBepInExInstalled(It.IsAny<GameInstallation>())).Returns(true);
        var modMgr = new Mock<IModManager>().Object;
        var launcher = new GameLauncher(bepMock.Object, modMgr);
        var game = CreateTempGameInstall(createExe: false);

        var result = await launcher.LaunchVanilla(game);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Stop_ReturnsError_WhenNotRunning()
    {
        var bepMock = new Mock<IBepInExManager>();
        var modMgr = new Mock<IModManager>().Object;
        var launcher = new GameLauncher(bepMock.Object, modMgr);
        var game = CreateTempGameInstall();

        var result = await launcher.Stop(game);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Stop_WithRunningProcess_ShouldAttemptGracefulShutdown()
    {
        // arrange
        var bepMock = new Mock<IBepInExManager>();
        var modMgr = new Mock<IModManager>().Object;
        var launcher = new GameLauncher(bepMock.Object, modMgr);
        var game = CreateTempGameInstall();

        // start a mock process that will exit gracefully
        var mockProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd" : "sh",
                Arguments = OperatingSystem.IsWindows() ? "/c exit" : "-c exit",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        mockProcess.Start();
        
        // manually add the process to the launcher's tracking
        var runningProcessesField = typeof(GameLauncher).GetField("_runningProcesses", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var runningProcesses = (System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Generic.List<Process>>)runningProcessesField!.GetValue(launcher)!;
        runningProcesses[game.InstallPath] = new System.Collections.Generic.List<Process> { mockProcess };

        try
        {
            // act
            var result = await launcher.Stop(game);

            // assert
            Assert.True(result.Success);
            Assert.Contains("stopped gracefully", result.Message);
        }
        finally
        {
            // cleanup
            if (!mockProcess.HasExited)
            {
                try { mockProcess.Kill(); } catch { }
            }
        }
    }

    [Fact]
    public async Task Stop_WithNonExitingProcess_ShouldFallbackToKill()
    {
        // arrange
        var bepMock = new Mock<IBepInExManager>();
        var modMgr = new Mock<IModManager>().Object;
        var launcher = new GameLauncher(bepMock.Object, modMgr);
        var game = CreateTempGameInstall();

        // start a long-running process that won't exit gracefully
        var mockProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd" : "sh",
                Arguments = OperatingSystem.IsWindows() ? "/c timeout 10" : "-c sleep 10",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        mockProcess.Start();
        
        // manually add the process to the launcher's tracking
        var runningProcessesField = typeof(GameLauncher).GetField("_runningProcesses", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var runningProcesses = (System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Generic.List<Process>>)runningProcessesField!.GetValue(launcher)!;
        runningProcesses[game.InstallPath] = new System.Collections.Generic.List<Process> { mockProcess };

        try
        {
            // act
            var result = await launcher.Stop(game);

            // assert
            Assert.True(result.Success);
            Assert.Contains("stopped gracefully", result.Message);
            Assert.True(mockProcess.HasExited);
        }
        finally
        {
            // cleanup
            if (!mockProcess.HasExited)
            {
                try { mockProcess.Kill(); } catch { }
            }
        }
    }

    [Fact]
    public void MirrorPluginDlls_CreatesSymlinksForFilesAndDirectories()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "lacesong_symlink_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        
        var gameInstall = new GameInstallation
        {
            Name = "Test Game",
            InstallPath = tempDir
        };

        // create bepinex plugins directory
        var pluginsDir = Path.Combine(tempDir, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsDir);

        // create mod directory with dll files and asset folders
        var modsDir = Path.Combine(tempDir, "mods");
        Directory.CreateDirectory(modsDir);
        var modDir = Path.Combine(modsDir, "test_mod");
        Directory.CreateDirectory(modDir);

        // create test files and directories
        var dllFile = Path.Combine(modDir, "test.dll");
        File.WriteAllText(dllFile, "test dll content");
        
        var texturesDir = Path.Combine(modDir, "textures");
        Directory.CreateDirectory(texturesDir);
        File.WriteAllText(Path.Combine(texturesDir, "texture.png"), "fake texture");
        
        var soundsDir = Path.Combine(modDir, "sounds");
        Directory.CreateDirectory(soundsDir);
        File.WriteAllText(Path.Combine(soundsDir, "sound.wav"), "fake sound");
        
        var configFile = Path.Combine(modDir, "config.txt");
        File.WriteAllText(configFile, "mod config");

        try
        {
            // act - use reflection to call the private MirrorPluginDlls method
            var modManagerType = typeof(ModManager);
            var mirrorMethod = modManagerType.GetMethod("MirrorPluginDlls", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            mirrorMethod!.Invoke(null, new object[] { "test_mod", modDir, gameInstall });

            // assert
            var pluginMirrorDir = Path.Combine(pluginsDir, "test_mod");
            Assert.True(Directory.Exists(pluginMirrorDir), "Plugin mirror directory should exist");

            // check that dll file is symlinked/copied
            var symlinkedDll = Path.Combine(pluginMirrorDir, "test.dll");
            Assert.True(File.Exists(symlinkedDll), "DLL file should be symlinked/copied");
            Assert.Equal("test dll content", File.ReadAllText(symlinkedDll));

            // check that asset directories are symlinked/copied
            var symlinkedTexturesDir = Path.Combine(pluginMirrorDir, "textures");
            Assert.True(Directory.Exists(symlinkedTexturesDir), "Textures directory should be symlinked/copied");
            Assert.True(File.Exists(Path.Combine(symlinkedTexturesDir, "texture.png")), "Texture file should exist in symlinked directory");

            var symlinkedSoundsDir = Path.Combine(pluginMirrorDir, "sounds");
            Assert.True(Directory.Exists(symlinkedSoundsDir), "Sounds directory should be symlinked/copied");
            Assert.True(File.Exists(Path.Combine(symlinkedSoundsDir, "sound.wav")), "Sound file should exist in symlinked directory");

            // check that config file is symlinked/copied
            var symlinkedConfig = Path.Combine(pluginMirrorDir, "config.txt");
            Assert.True(File.Exists(symlinkedConfig), "Config file should be symlinked/copied");
            Assert.Equal("mod config", File.ReadAllText(symlinkedConfig));
        }
        finally
        {
            // cleanup
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch { }
        }
    }
}

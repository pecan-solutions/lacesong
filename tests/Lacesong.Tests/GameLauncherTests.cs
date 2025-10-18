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
}

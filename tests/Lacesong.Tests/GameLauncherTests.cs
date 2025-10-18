/// <summary>
/// unit tests for GameLauncher
/// </summary>
using System;
using System.IO;
using System.Threading.Tasks;
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
}

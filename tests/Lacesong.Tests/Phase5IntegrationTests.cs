using Lacesong.Core.Services;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Moq;
using System.Threading.Tasks;
using Xunit;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Lacesong.Tests;

public class Phase5IntegrationTests
{
    [Fact]
    public async Task ConfigMerge_PreservesUserChanges()
    {
        // setup temp game install
        var tempDir = Path.Combine(Path.GetTempPath(), "lacesong_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var configDir = Path.Combine(tempDir, "BepInEx", "config");
        Directory.CreateDirectory(configDir);

        var oldIni = Path.Combine(configDir, "example.mod.cfg");
        await File.WriteAllTextAsync(oldIni, "[Section]\nKey=UserValue\n");
        var newIniTemp = Path.GetTempFileName();
        await File.WriteAllTextAsync(newIniTemp, "[Section]\nKey=Default\nNewKey=Added\n");

        var verificationMock = new Mock<IVerificationService>();
        verificationMock.Setup(v => v.CalculateChecksum(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("sum");
        var service = new ModConfigService(verificationMock.Object);

        var gameInstall = new GameInstallation { InstallPath = tempDir };
        var oldConfigs = await service.GetModConfigs("example.mod", gameInstall);
        var newConfigs = new List<ModConfig>
        {
            new ModConfig{ ModId="example.mod", ConfigPath=newIniTemp, ConfigType=ConfigType.Ini }
        };

        var result = await service.MergeConfigs("example.mod", oldConfigs, newConfigs, gameInstall);
        Assert.True(result.Success);

        var mergedPath = Path.Combine(configDir, "example.mod.cfg");
        var mergedContent = await File.ReadAllTextAsync(mergedPath);
        Assert.Contains("Key=UserValue", mergedContent); // user change preserved
        Assert.Contains("NewKey=Added", mergedContent);
    }

    [Fact]
    public async Task AutoUpdate_RollbackOnVerificationFailure()
    {
        var modManager = new Mock<IModManager>();
        var modIndex = new Mock<IModIndexService>();
        var configService = new Mock<IModConfigService>();
        var conflict = new Mock<IConflictDetectionService>();
        var verify = new Mock<IVerificationService>();
        var backup = new Mock<IBackupManager>();

        var thunder = new Mock<ThunderstoreService>();

        // simulate backup creation returns path
        backup.Setup(b => b.CreateBackup(It.IsAny<GameInstallation>(), It.IsAny<string>()))
              .ReturnsAsync(OperationResult.SuccessResult("ok", "backup_path"));
        // simulate restore
        backup.Setup(b => b.RestoreBackup("backup_path", It.IsAny<GameInstallation>()))
              .ReturnsAsync(OperationResult.SuccessResult("restored"));
        // simulate install failure? We'll succeed install
        modManager.Setup(m => m.InstallModFromZip(It.IsAny<string>(), It.IsAny<GameInstallation>()))
                  .ReturnsAsync(OperationResult.SuccessResult());
        // verification fails
        var modInfo = new ModInfo{ Id="mod", Version="2.0"};
        modManager.Setup(m => m.GetModInfo("mod", It.IsAny<GameInstallation>())).ReturnsAsync(modInfo);
        // conflict returns error -> verification will detect critical conflict
        conflict.Setup(c => c.DetectConflicts(It.IsAny<GameInstallation>(), It.IsAny<ModInfo>())).ReturnsAsync(new List<ModConflict>{
            new ModConflict{ConflictType=ConflictType.FileConflict, Severity=ConflictSeverity.Error, ConflictingMods=new List<string>{"mod"}}
        });

        var service = new ModUpdateService(modIndex.Object, modManager.Object, configService.Object, conflict.Object, verify.Object, backup.Object);
        var game = new GameInstallation{ InstallPath="/tmp" };
        var update = new ModUpdate{ ModId="mod", CurrentVersion="1.0", AvailableVersion="2.0", DownloadUrl="url", UpdateType=UpdateType.Major };
        var res = await service.InstallUpdate(update, game);
        Assert.False(res.Success);
        backup.Verify(b => b.RestoreBackup("backup_path", game), Times.Once);
    }

    [Fact]
    public async Task ConflictDetection_DuplicateDllFlagged()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "lacesong_mods_"+Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var modsDir = Path.Combine(tempDir, "mods");
        Directory.CreateDirectory(modsDir);
        var modAPath = Path.Combine(modsDir, "modA");
        var modBPath = Path.Combine(modsDir, "modB");
        Directory.CreateDirectory(Path.Combine(modAPath, "plugins"));
        Directory.CreateDirectory(Path.Combine(modBPath, "plugins"));
        File.WriteAllText(Path.Combine(modAPath, "plugins", "dup.dll"), "a");
        File.WriteAllText(Path.Combine(modBPath, "plugins", "dup.dll"), "b");

        var modManager = new Mock<IModManager>();
        modManager.Setup(m => m.GetInstalledMods(It.IsAny<GameInstallation>())).ReturnsAsync(new List<ModInfo>{
            new ModInfo{ Id="modA", Name="A" },
            new ModInfo{ Id="modB", Name="B" }
        });
        // dependency resolver not used
        var dep = new Mock<IDependencyResolver>();
        dep.Setup(d => d.ResolveDependencies(It.IsAny<ModInfo>(), It.IsAny<GameInstallation>()))
           .ReturnsAsync(new DependencyResolution{ Conflicts = new List<DependencyConflict>() });

        var conflictService = new ConflictDetectionService(modManager.Object, dep.Object);
        var game = new GameInstallation{ InstallPath=tempDir, ModDirectory="mods" };
        var conflicts = await conflictService.DetectConflicts(game);
        var fileConflict = conflicts.FirstOrDefault(c => c.ConflictType==ConflictType.FileConflict);
        Assert.NotNull(fileConflict);
        Assert.Contains("dup.dll", fileConflict!.Description);
        Assert.Equal(ConflictSeverity.Critical, fileConflict.Severity);
    }
}

using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Core.Services;
using System.Text.Json;
using Xunit;

namespace Lacesong.Tests;

/// <summary>
/// tests for game detection functionality
/// </summary>
public class GameDetectorTests
{
    private readonly IGameDetector _gameDetector;

    public GameDetectorTests()
    {
        _gameDetector = new GameDetector();
    }

    [Fact]
    public async Task DetectGameInstall_WithValidPath_ShouldReturnGameInstallation()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            // create a mock game executable for the current platform
            string executablePath;
            
            if (PlatformDetector.IsWindows)
            {
                executablePath = Path.Combine(tempDir, "Hollow Knight Silksong.exe");
                File.WriteAllText(executablePath, "mock executable");
            }
            else if (PlatformDetector.IsMacOS)
            {
                // create .app bundle structure
                var appBundlePath = Path.Combine(tempDir, "Hollow Knight Silksong.app");
                var contentsPath = Path.Combine(appBundlePath, "Contents");
                var macOSPath = Path.Combine(contentsPath, "MacOS");
                Directory.CreateDirectory(macOSPath);
                
                executablePath = Path.Combine(macOSPath, "Hollow Knight Silksong");
                File.WriteAllText(executablePath, "#!/bin/bash\necho 'mock executable'\n");
                
                // set executable permission on macos
                var chmod = System.Diagnostics.Process.Start("chmod", $"+x \"{executablePath}\"");
                chmod?.WaitForExit();
            }
            else // linux
            {
                executablePath = Path.Combine(tempDir, "Hollow Knight Silksong");
                File.WriteAllText(executablePath, "#!/bin/bash\necho 'mock executable'\n");
                
                // set executable permission on linux
                var chmod = System.Diagnostics.Process.Start("chmod", $"+x \"{executablePath}\"");
                chmod?.WaitForExit();
            }

            // act
            var result = await _gameDetector.DetectGameInstall(tempDir);

            // assert
            Assert.NotNull(result);
            Assert.Equal("Hollow Knight: Silksong", result.Name);
            Assert.Equal(tempDir, result.InstallPath);
            Assert.True(result.IsValid);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DetectGameInstall_WithInvalidPath_ShouldFallbackToAutomaticDetection()
    {
        // arrange
        var invalidPath = Path.Combine(Path.GetTempPath(), "nonexistent_directory");

        // act
        var result = await _gameDetector.DetectGameInstall(invalidPath);

        // assert
        // the method should fallback to automatic detection, so it might find a game
        // or return null if no games are found on the system
        // we just verify the method doesn't throw an exception
        Assert.True(result == null || result.IsValid);
    }

    [Fact]
    public void ValidateGameInstall_WithValidInstallation_ShouldReturnTrue()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var executablePath = Path.Combine(tempDir, "Hollow Knight Silksong.exe");
            File.WriteAllText(executablePath, "mock executable");

            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "Hollow Knight Silksong.exe"
            };

            // act
            var result = _gameDetector.ValidateGameInstall(gameInstall);

            // assert
            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateGameInstall_WithInvalidInstallation_ShouldReturnFalse()
    {
        // arrange
        var gameInstall = new GameInstallation
        {
            InstallPath = "nonexistent_path",
            Executable = "nonexistent.exe"
        };

        // act
        var result = _gameDetector.ValidateGameInstall(gameInstall);

        // assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetSupportedGames_ShouldReturnDefaultGames()
    {
        // act
        var games = await _gameDetector.GetSupportedGames();

        // assert
        Assert.NotEmpty(games);
        Assert.Contains(games, g => g.Name == "Hollow Knight: Silksong");
    }
}

/// <summary>
/// tests for bepinex management functionality
/// </summary>
public class BepInExManagerTests
{
    private readonly IBepInExManager _bepinexManager;

    public BepInExManagerTests()
    {
        _bepinexManager = new BepInExManager();
    }

    [Fact]
    public void IsBepInExInstalled_WithNoInstallation_ShouldReturnFalse()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe"
            };

            // act
            var result = _bepinexManager.IsBepInExInstalled(gameInstall);

            // assert
            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IsBepInExInstalled_WithValidInstallation_ShouldReturnTrue()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            // create mock bepinex structure
            var bepinexDir = Path.Combine(tempDir, "BepInEx");
            var coreDir = Path.Combine(bepinexDir, "core");
            Directory.CreateDirectory(coreDir);
            
            File.WriteAllText(Path.Combine(coreDir, "BepInEx.Core.dll"), "mock dll");
            File.WriteAllText(Path.Combine(coreDir, "BepInEx.dll"), "mock dll");

            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe"
            };

            // act
            var result = _bepinexManager.IsBepInExInstalled(gameInstall);

            // assert
            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetInstalledBepInExVersion_WithNoInstallation_ShouldReturnNull()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe"
            };

            // act
            var result = _bepinexManager.GetInstalledBepInExVersion(gameInstall);

            // assert
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

/// <summary>
/// tests for mod management functionality
/// </summary>
public class ModManagerTests
{
    private readonly IModManager _modManager;

    public ModManagerTests()
    {
        _modManager = new ModManager(new BepInExManager());
    }

    [Fact]
    public async Task GetInstalledMods_WithNoMods_ShouldReturnEmptyList()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe",
                ModDirectory = "BepInEx/plugins"
            };

            // act
            var result = await _modManager.GetInstalledMods(gameInstall);

            // assert
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GetModInfo_WithNonExistentMod_ShouldReturnNull()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe",
                ModDirectory = "BepInEx/plugins"
            };

            // act
            var result = await _modManager.GetModInfo("nonexistent-mod", gameInstall);

            // assert
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GetModInfo_WithValidMod_ShouldReturnModInfo()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var modDir = Path.Combine(tempDir, "BepInEx", "plugins", "test-mod");
            Directory.CreateDirectory(modDir);
            
            var modInfo = new ModInfo
            {
                Id = "test-mod",
                Name = "Test Mod",
                Version = "1.0.0",
                Description = "A test mod",
                Author = "Test Author",
                IsInstalled = true,
                IsEnabled = true
            };

            var modInfoJson = JsonSerializer.Serialize(modInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(modDir, "modinfo.json"), modInfoJson);

            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe",
                ModDirectory = "BepInEx/plugins"
            };

            // act
            var result = await _modManager.GetModInfo("test-mod", gameInstall);

            // assert
            Assert.NotNull(result);
            Assert.Equal("test-mod", result.Id);
            Assert.Equal("Test Mod", result.Name);
            Assert.Equal("1.0.0", result.Version);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

/// <summary>
/// tests for backup management functionality
/// </summary>
public class BackupManagerTests
{
    private readonly IBackupManager _backupManager;

    public BackupManagerTests()
    {
        _backupManager = new BackupManager();
    }

    [Fact]
    public async Task ListBackups_WithNoBackups_ShouldReturnEmptyList()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe"
            };

            // act
            var result = await _backupManager.ListBackups(gameInstall);

            // assert
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CreateBackup_WithValidGameInstall_ShouldCreateBackup()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var executablePath = Path.Combine(tempDir, "test.exe");
            File.WriteAllText(executablePath, "mock executable");

            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe"
            };

            // act
            var result = await _backupManager.CreateBackup(gameInstall, "test-backup");

            // assert
            Assert.True(result.Success);
            Assert.Contains("test-backup", result.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RestoreBackup_WithNonExistentFile_ShouldReturnError()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var executablePath = Path.Combine(tempDir, "test.exe");
            File.WriteAllText(executablePath, "mock executable");

            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe"
            };

            // act
            var result = await _backupManager.RestoreBackup("nonexistent-backup.lcb", gameInstall);

            // assert
            Assert.False(result.Success);
            Assert.Contains("does not exist", result.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

/// <summary>
/// tests for manifest parsing functionality
/// </summary>
public class ManifestParsingTests
{
    [Fact]
    public void ParseManifest_WithValidJson_ShouldDeserializeCorrectly()
    {
        // arrange
        var manifestJson = """
        {
            "name": "test-mod",
            "version": "1.0.0",
            "description": "A test mod",
            "author": "Test Author",
            "dependencies": ["dependency1", "dependency2"]
        }
        """;

        // act
        var modInfo = JsonSerializer.Deserialize<ModInfo>(manifestJson);

        // assert
        Assert.NotNull(modInfo);
        Assert.Equal("test-mod", modInfo.Name);
        Assert.Equal("1.0.0", modInfo.Version);
        Assert.Equal("A test mod", modInfo.Description);
        Assert.Equal("Test Author", modInfo.Author);
        Assert.Equal(2, modInfo.Dependencies.Count);
        Assert.Contains("dependency1", modInfo.Dependencies);
        Assert.Contains("dependency2", modInfo.Dependencies);
    }

    [Fact]
    public void SerializeModInfo_WithValidData_ShouldSerializeCorrectly()
    {
        // arrange
        var modInfo = new ModInfo
        {
            Id = "test-mod",
            Name = "Test Mod",
            Version = "1.0.0",
            Description = "A test mod",
            Author = "Test Author",
            Dependencies = new List<string> { "dependency1", "dependency2" },
            IsInstalled = true,
            IsEnabled = true
        };

        // act
        var json = JsonSerializer.Serialize(modInfo, new JsonSerializerOptions { WriteIndented = true });

        // assert
        Assert.NotNull(json);
        Assert.Contains("test-mod", json);
        Assert.Contains("Test Mod", json);
        Assert.Contains("1.0.0", json);
        Assert.Contains("dependency1", json);
        Assert.Contains("dependency2", json);
    }

    [Fact]
    public void ParseGameInstallation_WithValidData_ShouldDeserializeCorrectly()
    {
        // arrange
        var gameJson = """
        {
            "name": "Hollow Knight: Silksong",
            "id": "hollow-knight-silksong",
            "installPath": "C:\\Games\\Hollow Knight Silksong",
            "executable": "Hollow Knight Silksong.exe",
            "steamAppId": "1030300",
            "bepInExVersion": "5.4.22",
            "modDirectory": "BepInEx/plugins",
            "isValid": true,
            "detectedBy": "Steam"
        }
        """;

        // act
        var gameInstall = JsonSerializer.Deserialize<GameInstallation>(gameJson);

        // assert
        Assert.NotNull(gameInstall);
        Assert.Equal("Hollow Knight: Silksong", gameInstall.Name);
        Assert.Equal("hollow-knight-silksong", gameInstall.Id);
        Assert.Equal("C:\\Games\\Hollow Knight Silksong", gameInstall.InstallPath);
        Assert.Equal("Hollow Knight Silksong.exe", gameInstall.Executable);
        Assert.Equal("1030300", gameInstall.SteamAppId);
        Assert.Equal("5.4.22", gameInstall.BepInExVersion);
        Assert.Equal("BepInEx/plugins", gameInstall.ModDirectory);
        Assert.True(gameInstall.IsValid);
        Assert.Equal("Steam", gameInstall.DetectedBy);
    }
}

/// <summary>
/// tests for operation result functionality
/// </summary>
public class OperationResultTests
{
    [Fact]
    public void SuccessResult_WithMessage_ShouldCreateSuccessResult()
    {
        // act
        var result = OperationResult.SuccessResult("Operation completed");

        // assert
        Assert.True(result.Success);
        Assert.Equal("Operation completed", result.Message);
        Assert.Null(result.Error);
    }

    [Fact]
    public void SuccessResult_WithMessageAndData_ShouldCreateSuccessResultWithData()
    {
        // arrange
        var testData = new { TestProperty = "test value" };

        // act
        var result = OperationResult.SuccessResult("Operation completed", testData);

        // assert
        Assert.True(result.Success);
        Assert.Equal("Operation completed", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(testData, result.Data);
    }

    [Fact]
    public void ErrorResult_WithError_ShouldCreateErrorResult()
    {
        // act
        var result = OperationResult.ErrorResult("Something went wrong");

        // assert
        Assert.False(result.Success);
        Assert.Equal("Operation failed", result.Message);
        Assert.Equal("Something went wrong", result.Error);
    }

    [Fact]
    public void ErrorResult_WithErrorAndMessage_ShouldCreateErrorResultWithCustomMessage()
    {
        // act
        var result = OperationResult.ErrorResult("Something went wrong", "Custom error message");

        // assert
        Assert.False(result.Success);
        Assert.Equal("Custom error message", result.Message);
        Assert.Equal("Something went wrong", result.Error);
    }
}

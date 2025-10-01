using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Core.Services;
using System.Text.Json;
using Xunit;

namespace Lacesong.Tests;

/// <summary>
/// tests for dependency resolution functionality
/// </summary>
public class DependencyResolverTests
{
    private readonly IDependencyResolver _dependencyResolver;
    private readonly IModManager _modManager;
    private readonly IBepInExManager _bepinexManager;

    public DependencyResolverTests()
    {
        _bepinexManager = new BepInExManager();
        _modManager = new ModManager(_bepinexManager);
        _dependencyResolver = new DependencyResolver(_modManager, _bepinexManager);
    }

    [Fact]
    public async Task ResolveDependencies_WithNoDependencies_ShouldReturnValidResolution()
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

            var modInfo = new ModInfo
            {
                Id = "test-mod",
                Name = "Test Mod",
                Version = "1.0.0",
                Dependencies = new List<string>()
            };

            // act
            var result = await _dependencyResolver.ResolveDependencies(modInfo, gameInstall);

            // assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Empty(result.Conflicts);
            Assert.Empty(result.Missing);
            Assert.True(result.BepInExCompatible);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ResolveDependencies_WithBepInExRequirement_ShouldCheckCompatibility()
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

            var modInfo = new ModInfo
            {
                Id = "test-mod",
                Name = "Test Mod",
                Version = "1.0.0",
                Description = "A test mod with no specific requirements",
                Dependencies = new List<string>()
            };

            // act
            var result = await _dependencyResolver.ResolveDependencies(modInfo, gameInstall);

            // assert
            Assert.NotNull(result);
            Assert.NotNull(result.BepInExVersion);
            Assert.True(result.BepInExCompatible); // should be compatible since no specific requirement is specified
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ResolveDependencies_WithMissingDependency_ShouldReturnMissingDependency()
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

            var modInfo = new ModInfo
            {
                Id = "test-mod",
                Name = "Test Mod",
                Version = "1.0.0",
                Dependencies = new List<string> { "required-dependency" }
            };

            // act
            var result = await _dependencyResolver.ResolveDependencies(modInfo, gameInstall);

            // assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Single(result.Missing);
            Assert.Equal("required-dependency", result.Missing[0].Id);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task InstallMissingDependencies_WithMissingDependencies_ShouldReturnError()
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

            var resolution = new DependencyResolution
            {
                Missing = new List<ModDependency>
                {
                    new ModDependency { Id = "missing-mod", Version = "1.0.0" }
                }
            };

            // act
            var result = await _dependencyResolver.InstallMissingDependencies(resolution, gameInstall);

            // assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("not found", result.Error ?? "");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

/// <summary>
/// tests for signature and checksum verification functionality
/// </summary>
public class VerificationServiceTests
{
    private readonly IVerificationService _verificationService;

    public VerificationServiceTests()
    {
        _verificationService = new VerificationService();
    }

    [Fact]
    public async Task VerifyChecksum_WithValidFile_ShouldCalculateChecksum()
    {
        // arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        await File.WriteAllTextAsync(tempFile, "test content");
        
        try
        {
            // act
            var checksum = await _verificationService.CalculateChecksum(tempFile);

            // assert
            Assert.NotNull(checksum);
            Assert.NotEmpty(checksum);
            Assert.Equal(64, checksum.Length); // SHA256 hex string length
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task VerifyChecksum_WithMatchingChecksum_ShouldReturnSuccess()
    {
        // arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        var content = "test content";
        await File.WriteAllTextAsync(tempFile, content);
        
        var expectedChecksum = await _verificationService.CalculateChecksum(tempFile);
        
        try
        {
            // act
            var result = await _verificationService.VerifyChecksum(tempFile, expectedChecksum);

            // assert
            Assert.NotNull(result);
            Assert.True(result.Passed);
            Assert.Equal(ValidationType.Checksum, result.Type);
            Assert.Contains("successful", result.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task VerifyChecksum_WithNonMatchingChecksum_ShouldReturnFailure()
    {
        // arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        await File.WriteAllTextAsync(tempFile, "test content");
        
        try
        {
            // act
            var result = await _verificationService.VerifyChecksum(tempFile, "invalid_checksum");

            // assert
            Assert.NotNull(result);
            Assert.False(result.Passed);
            Assert.Equal(ValidationType.Checksum, result.Type);
            Assert.Contains("failed", result.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task VerifyFileIntegrity_WithValidFile_ShouldReturnSuccess()
    {
        // arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        await File.WriteAllTextAsync(tempFile, "test content");
        
        try
        {
            // act
            var result = await _verificationService.VerifyFileIntegrity(tempFile);

            // assert
            Assert.NotNull(result);
            Assert.True(result.Passed);
            Assert.Equal(ValidationType.FileIntegrity, result.Type);
            Assert.Contains("passed", result.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task VerifyFileIntegrity_WithNonExistentFile_ShouldReturnFailure()
    {
        // arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), "nonexistent_file.txt");

        // act
        var result = await _verificationService.VerifyFileIntegrity(nonExistentFile);

        // assert
        Assert.NotNull(result);
        Assert.False(result.Passed);
        Assert.Equal(ValidationType.FileIntegrity, result.Type);
        Assert.Contains("does not exist", result.Message);
    }
}

/// <summary>
/// tests for installation staging functionality
/// </summary>
public class InstallationStagerTests
{
    private readonly IInstallationStager _installationStager;
    private readonly IVerificationService _verificationService;
    private readonly IDependencyResolver _dependencyResolver;

    public InstallationStagerTests()
    {
        _verificationService = new VerificationService();
        var bepInExManager = new BepInExManager();
        _dependencyResolver = new DependencyResolver(new ModManager(bepInExManager), bepInExManager);
        _installationStager = new InstallationStager(_verificationService, _dependencyResolver);
    }

    [Fact]
    public async Task CreateStage_WithValidTargetPath_ShouldCreateStage()
    {
        // arrange
        var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // act
        var stage = await _installationStager.CreateStage(targetPath);

        // assert
        Assert.NotNull(stage);
        Assert.NotEmpty(stage.StageId);
        Assert.Equal(targetPath, stage.TargetPath);
        Assert.True(Directory.Exists(stage.TempPath));
        Assert.Equal(InstallationStageStatus.Pending, stage.Status);

        // cleanup
        await _installationStager.CleanupStage(stage);
    }

    [Fact]
    public async Task StageFiles_WithValidFiles_ShouldStageFiles()
    {
        // arrange
        var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var stage = await _installationStager.CreateStage(targetPath);
        
        var tempFile1 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        var tempFile2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        
        await File.WriteAllTextAsync(tempFile1, "test content 1");
        await File.WriteAllTextAsync(tempFile2, "test content 2");
        
        try
        {
            // act
            var result = await _installationStager.StageFiles(stage, new List<string> { tempFile1, tempFile2 });

            // assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(2, stage.Files.Count);
            Assert.Equal(InstallationStageStatus.Validating, stage.Status);
        }
        finally
        {
            File.Delete(tempFile1);
            File.Delete(tempFile2);
            await _installationStager.CleanupStage(stage);
        }
    }

    [Fact]
    public async Task ValidateStage_WithValidStagedFiles_ShouldPassValidation()
    {
        // arrange
        var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var stage = await _installationStager.CreateStage(targetPath);
        
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        await File.WriteAllTextAsync(tempFile, "test content");
        
        await _installationStager.StageFiles(stage, new List<string> { tempFile });
        
        try
        {
            // act
            var validationResults = await _installationStager.ValidateStage(stage);

            // assert
            Assert.NotNull(validationResults);
            Assert.NotEmpty(validationResults);
            Assert.True(validationResults.All(r => r.Passed));
            Assert.Equal(InstallationStageStatus.Ready, stage.Status);
        }
        finally
        {
            File.Delete(tempFile);
            await _installationStager.CleanupStage(stage);
        }
    }

    [Fact]
    public async Task RollbackStage_WithStagedFiles_ShouldRollback()
    {
        // arrange
        var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var stage = await _installationStager.CreateStage(targetPath);
        
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        await File.WriteAllTextAsync(tempFile, "test content");
        
        await _installationStager.StageFiles(stage, new List<string> { tempFile });
        
        try
        {
            // act
            var result = await _installationStager.RollbackStage(stage);

            // assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(InstallationStageStatus.RolledBack, stage.Status);
        }
        finally
        {
            File.Delete(tempFile);
            await _installationStager.CleanupStage(stage);
        }
    }

    [Fact]
    public async Task CleanupStage_WithStagedFiles_ShouldCleanup()
    {
        // arrange
        var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var stage = await _installationStager.CreateStage(targetPath);
        
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        await File.WriteAllTextAsync(tempFile, "test content");
        
        await _installationStager.StageFiles(stage, new List<string> { tempFile });
        
        try
        {
            // act
            var result = await _installationStager.CleanupStage(stage);

            // assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.False(Directory.Exists(stage.TempPath));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

/// <summary>
/// tests for permissions service functionality
/// </summary>
public class PermissionsServiceTests
{
    private readonly IPermissionsService _permissionsService;

    public PermissionsServiceTests()
    {
        _permissionsService = new PermissionsService();
    }

    [Fact]
    public async Task CheckPermissions_WithValidGameInstall_ShouldReturnPermissions()
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
            var permissions = await _permissionsService.CheckPermissions(gameInstall);

            // assert
            Assert.NotNull(permissions);
            Assert.True(permissions.CanWriteToGameDirectory);
            Assert.False(permissions.CanCreateSystemFiles); // should be false in temp directory
            Assert.False(permissions.IsElevated); // should be false in test environment
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RequiresElevation_WithInstallBepInEx_ShouldCheckPermissions()
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
            var requiresElevation = await _permissionsService.RequiresElevation("install-bepinex", gameInstall);

            // assert
            Assert.False(requiresElevation); // should not require elevation in temp directory
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RequiresElevation_WithSystemDirectory_ShouldRequireElevation()
    {
        // arrange
        var systemDir = OperatingSystem.IsWindows() ? @"C:\Program Files" : "/usr";
        var gameInstall = new GameInstallation
        {
            InstallPath = systemDir,
            Executable = "test.exe"
        };

        // act
        var requiresElevation = await _permissionsService.RequiresElevation("install-bepinex", gameInstall);

        // assert
        Assert.True(requiresElevation); // should require elevation in system directory
    }
}

/// <summary>
/// tests for enhanced backup functionality with restore points
/// </summary>
public class EnhancedBackupManagerTests
{
    private readonly IBackupManager _backupManager;

    public EnhancedBackupManagerTests()
    {
        _backupManager = new BackupManager();
    }

    [Fact]
    public async Task CreateRestorePoint_WithValidGameInstall_ShouldCreateRestorePoint()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var executablePath = Path.Combine(tempDir, "test.exe");
            await File.WriteAllTextAsync(executablePath, "mock executable");

            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe"
            };

            // act
            var result = await _backupManager.CreateBackup(gameInstall, "test-restore-point");

            // assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Contains("test-restore-point", result.Message);
            Assert.NotNull(result.Data);
            Assert.IsType<RestorePoint>(result.Data);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ListRestorePoints_WithNoRestorePoints_ShouldReturnEmptyList()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var executablePath = Path.Combine(tempDir, "test.exe");
            await File.WriteAllTextAsync(executablePath, "mock executable");

            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe"
            };

            // act
            var restorePoints = await _backupManager.ListBackups(gameInstall);

            // assert
            Assert.NotNull(restorePoints);
            Assert.Empty(restorePoints);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CreateAutomaticRestorePoint_WithValidGameInstall_ShouldCreateAutomaticRestorePoint()
    {
        // arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var executablePath = Path.Combine(tempDir, "test.exe");
            await File.WriteAllTextAsync(executablePath, "mock executable");

            var gameInstall = new GameInstallation
            {
                InstallPath = tempDir,
                Executable = "test.exe"
            };

            // act
            var result = await _backupManager.CreateBackup(gameInstall, "install-mod");

            // assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Contains("install-mod", result.Message);
            Assert.NotNull(result.Data);
            Assert.IsType<RestorePoint>(result.Data);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

/// <summary>
/// tests for new model serialization
/// </summary>
public class AdvancedModelTests
{
    [Fact]
    public void SerializeDependencyResolution_WithValidData_ShouldSerializeCorrectly()
    {
        // arrange
        var resolution = new DependencyResolution
        {
            BepInExVersion = "5.4.22",
            BepInExCompatible = true,
            IsValid = true,
            Resolved = new List<ModDependency>
            {
                new ModDependency { Id = "mod1", Version = "1.0.0", IsOptional = false }
            },
            Missing = new List<ModDependency>(),
            Conflicts = new List<DependencyConflict>()
        };

        // act
        var json = JsonSerializer.Serialize(resolution, new JsonSerializerOptions { WriteIndented = true });

        // assert
        Assert.NotNull(json);
        Assert.Contains("5.4.22", json);
        Assert.Contains("mod1", json);
        Assert.Contains("bepInExCompatible", json);
    }

    [Fact]
    public void SerializeInstallationStage_WithValidData_ShouldSerializeCorrectly()
    {
        // arrange
        var stage = new InstallationStage
        {
            StageId = "test-stage-id",
            TempPath = "/tmp/staging",
            TargetPath = "/target/path",
            Status = InstallationStageStatus.Pending,
            Files = new List<StagedFile>
            {
                new StagedFile
                {
                    SourcePath = "/source/file.dll",
                    TargetPath = "/tmp/staging/file.dll",
                    Checksum = "abc123",
                    Size = 1024,
                    IsExecutable = true
                }
            }
        };

        // act
        var json = JsonSerializer.Serialize(stage, new JsonSerializerOptions { WriteIndented = true });

        // assert
        Assert.NotNull(json);
        Assert.Contains("test-stage-id", json);
        Assert.Contains("Pending", json);
        Assert.Contains("file.dll", json);
        Assert.Contains("isExecutable", json);
    }

    [Fact]
    public void SerializeValidationResult_WithValidData_ShouldSerializeCorrectly()
    {
        // arrange
        var result = new ValidationResult
        {
            Type = ValidationType.Checksum,
            Passed = true,
            Message = "Checksum verification successful",
            Details = "SHA256 checksum matches",
            Timestamp = DateTime.UtcNow
        };

        // act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

        // assert
        Assert.NotNull(json);
        Assert.Contains("Checksum", json);
        Assert.Contains("successful", json);
        Assert.Contains("SHA256", json);
    }

    [Fact]
    public void SerializeUserPermissions_WithValidData_ShouldSerializeCorrectly()
    {
        // arrange
        var permissions = new UserPermissions
        {
            IsElevated = false,
            CanWriteToGameDirectory = true,
            CanCreateSystemFiles = false,
            CanModifyRegistry = false,
            RequiresElevation = false,
            ElevationReason = null
        };

        // act
        var json = JsonSerializer.Serialize(permissions, new JsonSerializerOptions { WriteIndented = true });

        // assert
        Assert.NotNull(json);
        Assert.Contains("isElevated", json);
        Assert.Contains("canWriteToGameDirectory", json);
        Assert.Contains("requiresElevation", json);
    }

    [Fact]
    public void SerializeRestorePoint_WithValidData_ShouldSerializeCorrectly()
    {
        // arrange
        var restorePoint = new RestorePoint
        {
            Id = "test-restore-id",
            Name = "Test Restore Point",
            Description = "A test restore point",
            Created = DateTime.UtcNow,
            BackupPath = "/path/to/backup.lcb",
            Size = 1024000,
            IsAutomatic = false,
            Tags = new List<string> { "test", "manual" },
            Mods = new List<ModInfo>(),
            BepInExVersion = "5.4.22"
        };

        // act
        var json = JsonSerializer.Serialize(restorePoint, new JsonSerializerOptions { WriteIndented = true });

        // assert
        Assert.NotNull(json);
        Assert.Contains("test-restore-id", json);
        Assert.Contains("Test Restore Point", json);
        Assert.Contains("5.4.22", json);
        Assert.Contains("test", json);
        Assert.Contains("manual", json);
    }
}

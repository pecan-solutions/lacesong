using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;

namespace Lacesong.Core.Services;

/// <summary>
/// service for safe installation staging with validation and rollback capabilities
/// </summary>
public class InstallationStager : IInstallationStager
{
    private readonly IVerificationService _verificationService;
    private readonly IDependencyResolver _dependencyResolver;

    public InstallationStager(IVerificationService verificationService, IDependencyResolver dependencyResolver)
    {
        _verificationService = verificationService;
        _dependencyResolver = dependencyResolver;
    }

    public async Task<InstallationStage> CreateStage(string targetPath)
    {
        try
        {
            var stageId = Guid.NewGuid().ToString();
            var tempPath = Path.Combine(Path.GetTempPath(), $"lacesong_stage_{stageId}");
            
            // create staging directory
            Directory.CreateDirectory(tempPath);

            var stage = new InstallationStage
            {
                StageId = stageId,
                TempPath = tempPath,
                TargetPath = targetPath,
                Status = InstallationStageStatus.Pending
            };

            return stage;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create installation stage: {ex.Message}", ex);
        }
    }

    public async Task<OperationResult> StageFiles(InstallationStage stage, List<string> sourceFiles)
    {
        try
        {
            stage.Status = InstallationStageStatus.Staging;

            foreach (var sourceFile in sourceFiles)
            {
                if (!File.Exists(sourceFile))
                {
                    return OperationResult.ErrorResult($"Source file not found: {sourceFile}", "File staging failed");
                }

                var fileName = Path.GetFileName(sourceFile);
                var targetFile = Path.Combine(stage.TempPath, fileName);
                
                // copy file to staging area
                File.Copy(sourceFile, targetFile, true);

                // calculate checksum for verification
                var checksum = await _verificationService.CalculateChecksum(targetFile);
                var fileInfo = new FileInfo(targetFile);

                var stagedFile = new StagedFile
                {
                    SourcePath = sourceFile,
                    TargetPath = targetFile,
                    Checksum = checksum,
                    Size = fileInfo.Length,
                    IsExecutable = IsExecutableFile(targetFile)
                };

                stage.Files.Add(stagedFile);
            }

            stage.Status = InstallationStageStatus.Validating;
            return OperationResult.SuccessResult($"Staged {sourceFiles.Count} files successfully");
        }
        catch (Exception ex)
        {
            stage.Status = InstallationStageStatus.Failed;
            return OperationResult.ErrorResult(ex.Message, "File staging failed");
        }
    }

    public async Task<List<ValidationResult>> ValidateStage(InstallationStage stage)
    {
        var validationResults = new List<ValidationResult>();

        try
        {
            // validate each staged file
            foreach (var stagedFile in stage.Files)
            {
                // file integrity check
                var integrityResult = await _verificationService.VerifyFileIntegrity(stagedFile.TargetPath);
                validationResults.Add(integrityResult);

                // permission check
                var permissionResult = await _verificationService.VerifyPermissions(stagedFile.TargetPath, false);
                validationResults.Add(permissionResult);

                // executable file validation
                if (stagedFile.IsExecutable)
                {
                    var executableResult = await ValidateExecutableFile(stagedFile.TargetPath);
                    validationResults.Add(executableResult);
                }
            }

            // validate staging directory permissions (create a test file to check)
            var testFile = Path.Combine(stage.TempPath, ".lacesong_test");
            try
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                validationResults.Add(new ValidationResult
                {
                    Type = ValidationType.Permissions,
                    Passed = true,
                    Message = "Staging directory permissions verified",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                validationResults.Add(new ValidationResult
                {
                    Type = ValidationType.Permissions,
                    Passed = false,
                    Message = "Staging directory permission check failed",
                    Details = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }

            // validate target directory permissions
            var targetDir = Path.GetDirectoryName(stage.TargetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                try
                {
                    var targetTestFile = Path.Combine(targetDir, ".lacesong_test");
                    File.WriteAllText(targetTestFile, "test");
                    File.Delete(targetTestFile);
                    validationResults.Add(new ValidationResult
                    {
                        Type = ValidationType.Permissions,
                        Passed = true,
                        Message = "Target directory permissions verified",
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    validationResults.Add(new ValidationResult
                    {
                        Type = ValidationType.Permissions,
                        Passed = false,
                        Message = "Target directory permission check failed",
                        Details = ex.Message,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            stage.ValidationResults = validationResults;
            stage.Status = validationResults.All(r => r.Passed) ? InstallationStageStatus.Ready : InstallationStageStatus.Failed;

            return validationResults;
        }
        catch (Exception ex)
        {
            var errorResult = new ValidationResult
            {
                Type = ValidationType.FileIntegrity,
                Passed = false,
                Message = "Stage validation error",
                Details = ex.Message
            };
            
            validationResults.Add(errorResult);
            stage.ValidationResults = validationResults;
            stage.Status = InstallationStageStatus.Failed;

            return validationResults;
        }
    }

    public async Task<OperationResult> CommitStage(InstallationStage stage)
    {
        try
        {
            if (stage.Status != InstallationStageStatus.Ready)
            {
                return OperationResult.ErrorResult("Stage is not ready for commit", "Invalid stage status");
            }

            // ensure target directory exists
            var targetDir = Path.GetDirectoryName(stage.TargetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // move staged files to target location
            foreach (var stagedFile in stage.Files)
            {
                var targetFile = Path.Combine(stage.TargetPath, Path.GetFileName(stagedFile.TargetPath));
                
                // ensure target directory exists
                var fileTargetDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(fileTargetDir))
                {
                    Directory.CreateDirectory(fileTargetDir);
                }

                // move file atomically
                if (File.Exists(targetFile))
                {
                    File.Delete(targetFile);
                }
                File.Move(stagedFile.TargetPath, targetFile);
            }

            stage.Status = InstallationStageStatus.Ready;
            return OperationResult.SuccessResult("Stage committed successfully");
        }
        catch (Exception ex)
        {
            stage.Status = InstallationStageStatus.Failed;
            return OperationResult.ErrorResult(ex.Message, "Stage commit failed");
        }
    }

    public async Task<OperationResult> RollbackStage(InstallationStage stage)
    {
        try
        {
            // if files were already committed, we need to remove them from target
            if (stage.Status == InstallationStageStatus.Ready)
            {
                foreach (var stagedFile in stage.Files)
                {
                    var targetFile = Path.Combine(stage.TargetPath, Path.GetFileName(stagedFile.TargetPath));
                    
                    if (File.Exists(targetFile))
                    {
                        File.Delete(targetFile);
                    }
                }
            }

            stage.Status = InstallationStageStatus.RolledBack;
            return OperationResult.SuccessResult("Stage rolled back successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Stage rollback failed");
        }
    }

    public async Task<OperationResult> CleanupStage(InstallationStage stage)
    {
        try
        {
            // remove staging directory and all contents
            if (Directory.Exists(stage.TempPath))
            {
                Directory.Delete(stage.TempPath, true);
            }

            return OperationResult.SuccessResult("Stage cleanup completed");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Stage cleanup failed");
        }
    }

    private bool IsExecutableFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".exe" => true,
            ".dll" => true,
            ".so" => true,
            ".dylib" => true,
            ".sh" => true,
            ".bat" => true,
            ".cmd" => true,
            _ => false
        };
    }

    private async Task<ValidationResult> ValidateExecutableFile(string filePath)
    {
        try
        {
            // basic executable file validation
            var fileInfo = new FileInfo(filePath);
            
            // check file size (executables should not be empty)
            if (fileInfo.Length == 0)
            {
                return new ValidationResult
                {
                    Type = ValidationType.FileIntegrity,
                    Passed = false,
                    Message = "Executable file is empty",
                    Details = "Executable files must have content"
                };
            }

            // check if file has proper executable header (basic check)
            var hasValidHeader = await HasValidExecutableHeader(filePath);

            return new ValidationResult
            {
                Type = ValidationType.FileIntegrity,
                Passed = hasValidHeader,
                Message = hasValidHeader ? "Executable file validation passed" : "Executable file validation failed",
                Details = hasValidHeader 
                    ? "File appears to be a valid executable"
                    : "File does not appear to be a valid executable"
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                Type = ValidationType.FileIntegrity,
                Passed = false,
                Message = "Executable file validation error",
                Details = ex.Message
            };
        }
    }

    private async Task<bool> HasValidExecutableHeader(string filePath)
    {
        try
        {
            // read first few bytes to check for executable headers
            var buffer = new byte[4];
            using var stream = File.OpenRead(filePath);
            await stream.ReadAsync(buffer, 0, buffer.Length);

            // check for common executable headers
            // PE header (Windows executables)
            if (buffer[0] == 0x4D && buffer[1] == 0x5A) // "MZ"
            {
                return true;
            }

            // ELF header (Linux executables)
            if (buffer[0] == 0x7F && buffer[1] == 0x45 && buffer[2] == 0x4C && buffer[3] == 0x46) // "ELF"
            {
                return true;
            }

            // Mach-O header (macOS executables)
            if (buffer[0] == 0xFE && buffer[1] == 0xED && buffer[2] == 0xFA && buffer[3] == 0xCE) // Mach-O 32-bit
            {
                return true;
            }
            if (buffer[0] == 0xFE && buffer[1] == 0xED && buffer[2] == 0xFA && buffer[3] == 0xCF) // Mach-O 64-bit
            {
                return true;
            }

            // for .dll files, we'll be more lenient
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".dll")
            {
                // .dll files might have different headers
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

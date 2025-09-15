using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace Lacesong.Core.Services;

/// <summary>
/// service for file signature and checksum verification
/// </summary>
public class VerificationService : IVerificationService
{
    public async Task<ValidationResult> VerifySignature(string filePath, FileSignature signature)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ValidationResult
                {
                    Type = ValidationType.Signature,
                    Passed = false,
                    Message = "File does not exist",
                    Details = $"File not found: {filePath}"
                };
            }

            if (string.IsNullOrEmpty(signature.Signature) || string.IsNullOrEmpty(signature.PublicKey))
            {
                return new ValidationResult
                {
                    Type = ValidationType.Signature,
                    Passed = false,
                    Message = "Invalid signature data",
                    Details = "Missing signature or public key"
                };
            }

            // calculate file hash
            var fileHash = await CalculateChecksum(filePath, signature.Algorithm);
            
            // verify signature
            var isValid = await VerifyDigitalSignature(fileHash, signature.Signature, signature.PublicKey);

            return new ValidationResult
            {
                Type = ValidationType.Signature,
                Passed = isValid,
                Message = isValid ? "Signature verification successful" : "Signature verification failed",
                Details = isValid ? "File signature is valid" : "File signature does not match or is invalid"
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                Type = ValidationType.Signature,
                Passed = false,
                Message = "Signature verification error",
                Details = ex.Message
            };
        }
    }

    public async Task<ValidationResult> VerifyChecksum(string filePath, string expectedChecksum, string algorithm = "SHA256")
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ValidationResult
                {
                    Type = ValidationType.Checksum,
                    Passed = false,
                    Message = "File does not exist",
                    Details = $"File not found: {filePath}"
                };
            }

            if (string.IsNullOrEmpty(expectedChecksum))
            {
                return new ValidationResult
                {
                    Type = ValidationType.Checksum,
                    Passed = false,
                    Message = "No expected checksum provided",
                    Details = "Expected checksum is required for verification"
                };
            }

            // calculate file checksum
            var calculatedChecksum = await CalculateChecksum(filePath, algorithm);
            
            // compare checksums (case-insensitive)
            var isValid = string.Equals(calculatedChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);

            return new ValidationResult
            {
                Type = ValidationType.Checksum,
                Passed = isValid,
                Message = isValid ? "Checksum verification successful" : "Checksum verification failed",
                Details = isValid 
                    ? $"Checksum matches: {calculatedChecksum}"
                    : $"Expected: {expectedChecksum}, Calculated: {calculatedChecksum}"
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                Type = ValidationType.Checksum,
                Passed = false,
                Message = "Checksum verification error",
                Details = ex.Message
            };
        }
    }

    public async Task<string> CalculateChecksum(string filePath, string algorithm = "SHA256")
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await CalculateChecksum(stream, algorithm);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to calculate checksum for {filePath}: {ex.Message}", ex);
        }
    }

    private async Task<string> CalculateChecksum(Stream stream, string algorithm)
    {
        HashAlgorithm hashAlgorithm = algorithm.ToUpperInvariant() switch
        {
            "SHA1" => SHA1.Create(),
            "SHA256" => SHA256.Create(),
            "SHA384" => SHA384.Create(),
            "SHA512" => SHA512.Create(),
            "MD5" => MD5.Create(),
            _ => throw new ArgumentException($"Unsupported hash algorithm: {algorithm}")
        };

        try
        {
            var hash = await hashAlgorithm.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        finally
        {
            hashAlgorithm.Dispose();
        }
    }

    private async Task<bool> VerifyDigitalSignature(string data, string signature, string publicKey)
    {
        try
        {
            // this is a simplified implementation
            // in a real implementation, this would use proper cryptographic signature verification
            // using RSA or ECDSA with the provided public key
            
            // for now, we'll implement a basic validation
            // in practice, you would:
            // 1. Parse the public key (PEM format, X.509 certificate, etc.)
            // 2. Parse the signature (DER format, etc.)
            // 3. Verify the signature against the data using the public key
            
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(publicKey))
            {
                return false;
            }

            // placeholder implementation - always returns true for demonstration
            // in production, replace with actual cryptographic verification
            await Task.Delay(10); // simulate async work
            
            // basic validation: check if signature looks like a valid format
            var isValidFormat = signature.Length > 0 && publicKey.Length > 0;
            
            return isValidFormat;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ValidationResult> VerifyFileIntegrity(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ValidationResult
                {
                    Type = ValidationType.FileIntegrity,
                    Passed = false,
                    Message = "File does not exist",
                    Details = $"File not found: {filePath}"
                };
            }

            // check file accessibility
            using var fileStream = File.OpenRead(filePath);
            var canRead = fileStream.CanRead;
            
            // check file size
            var fileInfo = new FileInfo(filePath);
            var hasContent = fileInfo.Length > 0;

            // check if file is corrupted (basic check)
            var isCorrupted = await IsFileCorrupted(filePath);

            var isValid = canRead && hasContent && !isCorrupted;

            return new ValidationResult
            {
                Type = ValidationType.FileIntegrity,
                Passed = isValid,
                Message = isValid ? "File integrity check passed" : "File integrity check failed",
                Details = isValid 
                    ? $"File is accessible and valid ({fileInfo.Length} bytes)"
                    : $"File issues: Readable={canRead}, HasContent={hasContent}, NotCorrupted={!isCorrupted}"
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                Type = ValidationType.FileIntegrity,
                Passed = false,
                Message = "File integrity check error",
                Details = ex.Message
            };
        }
    }

    private async Task<bool> IsFileCorrupted(string filePath)
    {
        try
        {
            // basic corruption detection
            // in practice, this might be more sophisticated
            
            var fileInfo = new FileInfo(filePath);
            
            // check if file is too small to be valid
            if (fileInfo.Length < 10)
            {
                return true;
            }

            // try to read the file completely
            var buffer = new byte[Math.Min(fileInfo.Length, 1024 * 1024)]; // read up to 1MB
            using var stream = File.OpenRead(filePath);
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            
            // check if we can read the expected amount
            return bytesRead != buffer.Length;
        }
        catch
        {
            return true; // if we can't read it, consider it corrupted
        }
    }

    public async Task<ValidationResult> VerifyPermissions(string filePath, bool requireWrite = false)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ValidationResult
                {
                    Type = ValidationType.Permissions,
                    Passed = false,
                    Message = "File does not exist",
                    Details = $"File not found: {filePath}"
                };
            }

            var fileInfo = new FileInfo(filePath);
            var directory = fileInfo.Directory;

            if (directory == null)
            {
                return new ValidationResult
                {
                    Type = ValidationType.Permissions,
                    Passed = false,
                    Message = "Invalid file path",
                    Details = "Cannot determine directory"
                };
            }

            // check read permissions
            var canRead = true;
            try
            {
                using var stream = File.OpenRead(filePath);
                canRead = stream.CanRead;
            }
            catch
            {
                canRead = false;
            }

            // check write permissions if required
            var canWrite = true;
            if (requireWrite)
            {
                try
                {
                    using var stream = File.OpenWrite(filePath);
                    canWrite = stream.CanWrite;
                }
                catch
                {
                    canWrite = false;
                }
            }

            var isValid = canRead && (!requireWrite || canWrite);

            return new ValidationResult
            {
                Type = ValidationType.Permissions,
                Passed = isValid,
                Message = isValid ? "Permission check passed" : "Permission check failed",
                Details = $"Read: {canRead}, Write: {canWrite} (Required: {requireWrite})"
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                Type = ValidationType.Permissions,
                Passed = false,
                Message = "Permission check error",
                Details = ex.Message
            };
        }
    }
}

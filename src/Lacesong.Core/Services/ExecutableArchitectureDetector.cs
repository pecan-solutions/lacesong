using Lacesong.Core.Models;

namespace Lacesong.Core.Services;

/// <summary>
/// service for detecting executable architecture by analyzing binary headers
/// </summary>
public static class ExecutableArchitectureDetector
{
    /// <summary>
    /// detects the architecture of an executable by analyzing its binary headers
    /// </summary>
    public static Architecture DetectArchitecture(string executablePath)
    {
        if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            return Architecture.X64; // default fallback

        try
        {
            var extension = Path.GetExtension(executablePath).ToLowerInvariant();
            
            return extension switch
            {
                ".exe" => DetectWindowsExecutableArchitecture(executablePath),
                ".app" => DetectMacOSAppBundleArchitecture(executablePath),
                "" => DetectUnixExecutableArchitecture(executablePath),
                _ => Architecture.X64 // default fallback
            };
        }
        catch
        {
            return Architecture.X64; // fallback on any error
        }
    }

    /// <summary>
    /// detects architecture of a windows executable by analyzing pe header
    /// </summary>
    private static Architecture DetectWindowsExecutableArchitecture(string executablePath)
    {
        try
        {
            using var fs = new FileStream(executablePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            // read dos header
            var dosSignature = reader.ReadUInt16();
            if (dosSignature != 0x5A4D) // "MZ"
                return Architecture.X64;

            // skip to pe header offset
            fs.Seek(60, SeekOrigin.Begin);
            var peOffset = reader.ReadUInt32();
            
            // seek to pe header
            fs.Seek(peOffset, SeekOrigin.Begin);
            var peSignature = reader.ReadUInt32();
            if (peSignature != 0x00004550) // "PE\0\0"
                return Architecture.X64;

            // read machine type from coff header
            var machine = reader.ReadUInt16();
            
            return machine switch
            {
                0x014c => Architecture.X86,  // IMAGE_FILE_MACHINE_I386
                0x8664 => Architecture.X64,   // IMAGE_FILE_MACHINE_AMD64
                0xAA64 => Architecture.X64,   // IMAGE_FILE_MACHINE_ARM64 -> map to x64 for bepinex compatibility
                _ => Architecture.X64
            };
        }
        catch
        {
            return Architecture.X64;
        }
    }

    /// <summary>
    /// detects architecture of a macos app bundle by analyzing the main executable
    /// </summary>
    private static Architecture DetectMacOSAppBundleArchitecture(string appBundlePath)
    {
        try
        {
            var baseName = Path.GetFileNameWithoutExtension(appBundlePath);
            var executablePath = Path.Combine(appBundlePath, "Contents", "MacOS", baseName);
            
            if (!File.Exists(executablePath))
                return Architecture.X64;

            return DetectMachOArchitecture(executablePath);
        }
        catch
        {
            return Architecture.X64;
        }
    }

    /// <summary>
    /// detects architecture of a unix executable by analyzing elf header
    /// </summary>
    private static Architecture DetectUnixExecutableArchitecture(string executablePath)
    {
        try
        {
            // check if it's a mach-o binary (macos)
            if (IsMachOBinary(executablePath))
                return DetectMachOArchitecture(executablePath);

            // check if it's an elf binary (linux)
            if (IsElfBinary(executablePath))
                return DetectElfArchitecture(executablePath);

            return Architecture.X64; // fallback
        }
        catch
        {
            return Architecture.X64;
        }
    }

    /// <summary>
    /// detects architecture of a mach-o binary
    /// </summary>
    private static Architecture DetectMachOArchitecture(string executablePath)
    {
        try
        {
            using var fs = new FileStream(executablePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            var magic = reader.ReadUInt32();
            
            // check for mach-o magic numbers
            if (magic == 0xFEEDFACF || magic == 0xFEEDFACE) // 64-bit or 32-bit mach-o
            {
                var cputype = reader.ReadUInt32();
                
                return cputype switch
                {
                    0x01000007 => Architecture.X64,   // CPU_TYPE_X86_64
                    0x0100000C => Architecture.X64,   // CPU_TYPE_ARM64 -> map to x64 for bepinex compatibility
                    0x00000007 => Architecture.X86,    // CPU_TYPE_I386
                    _ => Architecture.X64
                };
            }

            return Architecture.X64;
        }
        catch
        {
            return Architecture.X64;
        }
    }

    /// <summary>
    /// detects architecture of an elf binary
    /// </summary>
    private static Architecture DetectElfArchitecture(string executablePath)
    {
        try
        {
            using var fs = new FileStream(executablePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            var magic = reader.ReadUInt32();
            if (magic != 0x464C457F) // ELF magic "\x7FELF"
                return Architecture.X64;

            // seek to machine type field at offset 18
            fs.Seek(18, SeekOrigin.Begin);
            
            // read machine type
            var machine = reader.ReadUInt16();
            
            return machine switch
            {
                0x03 => Architecture.X86,   // EM_386
                0x3E => Architecture.X64,    // EM_X86_64
                0xB7 => Architecture.X64,   // EM_AARCH64 -> map to x64 for bepinex compatibility
                _ => Architecture.X64
            };
        }
        catch
        {
            return Architecture.X64;
        }
    }

    /// <summary>
    /// checks if a file is a mach-o binary
    /// </summary>
    private static bool IsMachOBinary(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            var magic = reader.ReadUInt32();
            return magic == 0xFEEDFACF || magic == 0xFEEDFACE || magic == 0xCAFEBABE;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// checks if a file is an elf binary
    /// </summary>
    private static bool IsElfBinary(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            var magic = reader.ReadUInt32();
            return magic == 0x464C457F; // ELF magic "\x7FELF"
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// converts architecture enum to string format used by bepinex downloads
    /// </summary>
    public static string GetArchitectureString(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            _ => "x64"
        };
    }
}

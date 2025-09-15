# Lacesong Core Library

This is the core library for the Lacesong mod management tool, providing essential APIs for game detection, mod installation, and management.

## Features

- **Game Detection**: Automatically detect game installations from Steam, Epic Games, GOG, and manual paths
- **BepInEx Management**: Install, configure, and manage BepInEx mod loader
- **Mod Management**: Install, uninstall, enable, and disable mods
- **Backup & Restore**: Create and 
restore backups of mod configurations
- **Robust Error Handling**: Comprehensive error handling with detailed operation results
- **Atomic Operations**: Safe file operations using temporary directories and atomic moves

## Core APIs

### Game Detection
- `DetectGameInstall(pathHint)` - Detect game installation automatically or from specific path
- `ValidateGameInstall(gameInstall)` - Validate if a game installation is complete
- `GetSupportedGames()` - Get list of supported games

### BepInEx Management
- `InstallBepInEx(gameInstall, options)` - Install BepInEx with configuration options
- `IsBepInExInstalled(gameInstall)` - Check if BepInEx is installed
- `GetInstalledBepInExVersion(gameInstall)` - Get installed BepInEx version
- `UninstallBepInEx(gameInstall)` - Uninstall BepInEx

### Mod Management
- `InstallModFromZip(source, gameInstall)` - Install mod from zip file or URL
- `UninstallMod(modId, gameInstall)` - Uninstall mod by ID
- `EnableMod(modId, gameInstall)` - Enable a disabled mod
- `DisableMod(modId, gameInstall)` - Disable an enabled mod
- `GetInstalledMods(gameInstall)` - Get list of installed mods
- `GetModInfo(modId, gameInstall)` - Get detailed mod information

### Backup & Restore
- `CreateBackup(gameInstall, backupName)` - Create backup of current configuration
- `RestoreBackup(backupPath, gameInstall)` - Restore from backup
- `ListBackups(gameInstall)` - List available backups
- `DeleteBackup(backupPath)` - Delete a backup

## Usage Example

```csharp
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Core.Services;

// Create services
var gameDetector = new GameDetector();
var bepinexManager = new BepInExManager();
var modManager = new ModManager();

// Detect game installation
var gameInstall = await gameDetector.DetectGameInstall();
if (gameInstall == null)
{
    Console.WriteLine("No game installation detected");
    return;
}

// Install BepInEx
var bepinexOptions = new BepInExInstallOptions
{
    Version = "5.4.22",
    ForceReinstall = false,
    BackupExisting = true
};

var result = await bepinexManager.InstallBepInEx(gameInstall, bepinexOptions);
if (result.Success)
{
    Console.WriteLine("BepInEx installed successfully");
}

// Install a mod
var modResult = await modManager.InstallModFromZip("path/to/mod.zip", gameInstall);
if (modResult.Success)
{
    Console.WriteLine("Mod installed successfully");
}
```

## Dependencies

- .NET 8.0
- Newtonsoft.Json
- System.IO.Compression
- Microsoft.Win32.Registry

## Testing

The library includes comprehensive unit tests covering:
- Game detection functionality
- BepInEx management operations
- Mod installation and management
- Backup and restore operations
- Manifest parsing and serialization
- Error handling and operation results

Run tests with:
```bash
dotnet test
```

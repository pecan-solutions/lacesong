# Cross-Platform Game Detection Implementation

This document describes the comprehensive cross-platform game detection system implemented for Lacesong, supporting Windows, macOS, and Linux platforms.

## Overview

The game detection system has been completely rewritten to support all three major platforms with automatic detection of game installations from various sources:

- **Steam** - Primary game distribution platform
- **GOG Galaxy** - DRM-free game platform
- **Epic Games Store** - Epic's game distribution platform
- **Xbox Game Pass** - Microsoft's subscription service (Windows only)

## Architecture

### PlatformDetector Service

A new `PlatformDetector` static service provides:

- **Platform Detection**: Automatically detects current OS (Windows, macOS, Linux)
- **Path Resolution**: Returns platform-specific installation paths for each game store
- **Executable Validation**: Validates executables based on platform conventions
- **Executable Naming**: Converts executable names for platform conventions

### GameDetector Service

The existing `GameDetector` service has been enhanced to:

- **Cross-Platform Support**: Works on all supported platforms
- **Store-Specific Detection**: Detects games from multiple store platforms
- **Path Flexibility**: Supports various installation directory structures
- **App Bundle Support**: Handles macOS .app bundles correctly

## Platform-Specific Implementation

### Windows

**Steam Detection:**
- Registry-based Steam installation path detection
- Library folders parsing from `libraryfolders.vdf`
- Support for multiple Steam library locations

**GOG Galaxy Detection:**
- Multiple registry location checking
- Common installation path fallbacks
- Support for various GOG installation structures

**Epic Games Detection:**
- Registry-based Epic launcher detection
- Manifest parsing for installed games
- Support for Epic's game directory structure

**Xbox Game Pass Detection:**
- Microsoft Store app detection
- Xbox app-specific paths
- WindowsApps directory scanning

### macOS

**Steam Detection:**
- `~/Library/Application Support/Steam` primary location
- Additional Steam library locations
- .app bundle support for Steam games

**GOG Galaxy Detection:**
- `~/Library/Application Support/GOG.com/Galaxy` primary location
- User Applications directory support
- .app bundle detection

**Epic Games Detection:**
- `~/Library/Application Support/Epic/EpicGamesLauncher` primary location
- Epic launcher .app bundle support
- Manifest-based game detection

**Xbox Game Pass:**
- Not available on macOS (correctly handled)

### Linux

**Steam Detection:**
- `~/.steam/steam` and `~/.local/share/Steam` primary locations
- System-wide Steam installation support
- Flatpak and Snap package support

**GOG Galaxy Detection:**
- `~/.local/share/GOG.com/Galaxy` primary location
- User Games directory support
- Various GOG installation structures

**Epic Games Detection:**
- `~/.local/share/Epic/EpicGamesLauncher` primary location
- Epic launcher installation detection
- Manifest-based game detection

**Xbox Game Pass:**
- Not available on Linux (correctly handled)

## Executable Detection

### Windows
- `.exe` file extension required
- PE header validation
- Registry-based validation

### macOS
- No extension required for executables
- `.app` bundle support (Contents/MacOS/executable)
- Mach-O header validation
- Executable permission checking

### Linux
- No extension required for executables
- ELF header validation
- Executable permission checking
- Various executable formats support

## Game-Specific Configuration

### Hollow Knight: Silksong

The system supports detection of Hollow Knight: Silksong with platform-specific configurations:

**Windows:**
- Executable: `Hollow Knight Silksong.exe`
- Mod Directory: `BepInEx/plugins`

**macOS:**
- Executable: `Hollow Knight Silksong` (or `.app` bundle)
- Mod Directory: `BepInEx/plugins`

**Linux:**
- Executable: `Hollow Knight Silksong`
- Mod Directory: `BepInEx/plugins`

## Detection Flow

1. **Platform Detection**: Automatically detect current platform
2. **Store Detection**: Check each supported store for installations
3. **Path Resolution**: Get platform-specific installation paths
4. **Game Scanning**: Search for supported games in each location
5. **Validation**: Validate found games and executables
6. **Result Ranking**: Return the first valid installation found

## Error Handling

- **Graceful Degradation**: Continue detection even if individual stores fail
- **Exception Handling**: Catch and log errors without stopping detection
- **Path Validation**: Verify directory and file existence before processing
- **Platform Fallbacks**: Provide fallback paths for each platform

## Testing

A test program (`test-detection.cs`) is provided to verify:

- Platform detection accuracy
- Path resolution for each store
- Executable name conversion
- Game detection functionality

## Future Enhancements

- **Additional Stores**: Support for more game distribution platforms
- **Game-Specific Paths**: Custom detection logic for specific games
- **User Configuration**: Allow users to specify custom installation paths
- **Detection Caching**: Cache detection results for improved performance

## Usage

```csharp
var detector = new GameDetector();
var game = await detector.DetectGameInstall();

if (game != null)
{
    Console.WriteLine($"Found {game.Name} at {game.InstallPath}");
    Console.WriteLine($"Detected by: {game.DetectedBy}");
}
```

The system automatically handles all platform-specific logic and provides a unified interface for game detection across all supported platforms.

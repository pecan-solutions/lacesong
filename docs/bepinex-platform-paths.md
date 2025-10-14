# BepInEx Platform-Specific Path Handling

## Overview
This document describes how BepInEx detection and installation works across different platforms (Windows, macOS, Linux).

## Path Structure

### Windows
- Game Installation: `C:\Program Files\Steam\steamapps\common\Hollow Knight Silksong\`
- Game Executable: `C:\Program Files\Steam\steamapps\common\Hollow Knight Silksong\Hollow Knight Silksong.exe`
- BepInEx Location: `C:\Program Files\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\`
- BepInEx DLL: `C:\Program Files\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\core\BepInEx.dll`

### macOS
- Game Installation: `~/Library/Application Support/Steam/steamapps/common/Hollow Knight Silksong/`
- Game Executable: `~/Library/Application Support/Steam/steamapps/common/Hollow Knight Silksong/Hollow Knight Silksong.app/Contents/MacOS/Hollow Knight Silksong`
- BepInEx Location: `~/Library/Application Support/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/`
- BepInEx DLL: `~/Library/Application Support/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/core/BepInEx.dll`

**Note**: On macOS, BepInEx is installed in the **same folder** as the `.app` bundle, **not inside it**.

### Linux
- Game Installation: `~/.local/share/Steam/steamapps/common/Hollow Knight Silksong/`
- Game Executable: `~/.local/share/Steam/steamapps/common/Hollow Knight Silksong/Hollow Knight Silksong`
- BepInEx Location: `~/.local/share/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/`
- BepInEx DLL: `~/.local/share/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/core/BepInEx.dll`

## Implementation Details

### GameDetector
The `GameDetector` service sets `GameInstallation.InstallPath` to:
- **Windows**: The folder containing the `.exe` file
- **macOS**: The folder containing the `.app` bundle (parent directory of the `.app`)
- **Linux**: The folder containing the executable

### BepInExManager
The `BepInExManager` uses a helper method `GetBepInExBaseDirectory()` that returns:
- `gameInstall.InstallPath` for all platforms

This ensures BepInEx is always installed in the correct location:
- Windows: Alongside the `.exe` in the game folder
- macOS: Alongside the `.app` bundle in the game folder
- Linux: Alongside the executable in the game folder

### Methods Updated
The following methods now use `GetBepInExBaseDirectory()` for consistent path handling:
1. `IsBepInExInstalled()` - Checks if BepInEx exists at the correct location
2. `GetInstalledBepInExVersion()` - Reads version from the correct DLL path
3. `GetBepInExVersionInfo()` - Retrieves detailed version info from correct paths
4. `InstallBepInEx()` - Extracts BepInEx to the correct base directory
5. `UninstallBepInEx()` - Removes BepInEx from the correct location
6. `ConfigureBepInEx()` - Creates config files in the correct location
7. `CreateBackup()` - Backs up BepInEx from the correct location

## Version Detection
BepInEx version is detected by reading file version information from:
1. Primary: `BepInEx/core/BepInEx.dll` (loader)
2. Fallback: `BepInEx/core/BepInEx.Core.dll` (core)

The version is read using `System.Diagnostics.FileVersionInfo.GetVersionInfo()` which works on all platforms.

## Testing
To verify BepInEx detection works correctly:

1. **Windows**: Ensure `BepInEx/core/BepInEx.dll` exists in the game folder
2. **macOS**: Ensure `BepInEx/core/BepInEx.dll` exists alongside the `.app` bundle
3. **Linux**: Ensure `BepInEx/core/BepInEx.dll` exists in the game folder

The version number should be correctly extracted and displayed in the UI.


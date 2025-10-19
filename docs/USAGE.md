# Lacesong - Mod Manager Usage Guide

## Overview

Lacesong is a cross-platform mod management tool for Unity/Mono games, starting with Hollow Knight: Silksong. It provides a simple, safe, and consistent way to install, manage, and update community mods using the BepInEx mod loader.

## Installation

### Prerequisites

- Windows 10/11 (64-bit)
- .NET 9.0 Runtime (included in installer)
- Hollow Knight: Silksong installed

### Download and Install

1. Download the latest Lacesong installer from [GitHub Releases](https://github.com/pecansolutions/lacesong/releases)
2. Run the installer and follow the setup wizard
3. Launch Lacesong from the Start Menu or Desktop shortcut

## Getting Started

### First Launch

When you first launch Lacesong, it will automatically:

1. **Detect your game installation** - Scans for Hollow Knight: Silksong
2. **Check for updates** - Verifies you have the latest version
3. **Navigate to Game Detection** - Shows the detection screen

### Game Detection

The Game Detection screen helps you locate your Hollow Knight: Silksong installation:

#### Automatic Detection
- Click **"🔍 Detect Games"** to scan your system
- Lacesong will check:
  - Steam Library folders
  - Epic Games Store installations
  - GOG Galaxy installations
  - Xbox Game Pass installations
  - Common installation paths

#### Manual Selection
- Click **"📁 Browse for Game"** if automatic detection fails
- Navigate to your game folder containing `Hollow Knight Silksong.exe`
- Select the folder and click OK

#### Proceeding
- Once a game is detected, it will appear in the list
- Select your game and click **"Continue"** to proceed to mod management

## BepInEx Installation

Before installing mods, you need BepInEx (the mod loader):

### Installation Process

1. **Navigate to BepInEx Install** - Use the sidebar or main navigation
2. **Check Status** - The screen shows if BepInEx is already installed
3. **Configure Options**:
   - **Version**: Select BepInEx version (default: 5.4.22)
   - **Force Reinstall**: Check if you want to reinstall over existing installation
   - **Create Backup**: Recommended - backs up existing installation
   - **Desktop Shortcut**: Optional - creates a shortcut to launch game with BepInEx

4. **Install** - Click **"📥 Install BepInEx"**
5. **Wait for Completion** - Installation progress is shown in the status area

### After Installation

- BepInEx files are installed to your game directory
- A `BepInEx` folder is created with the mod loader
- Configuration files are set up automatically
- You can now install mods

## Mod Management

The Mod Catalog is where you manage all your mods:

### Installing Mods

#### From File
1. Click **"📁 Install from File"**
2. Select a `.zip` file containing the mod
3. Lacesong will extract and install the mod automatically

#### From URL
1. Click **"🌐 Install from URL"**
2. Enter the download URL for the mod
3. Lacesong will download and install the mod

### Managing Installed Mods

The mod list shows all installed mods with:
- **Name and Description**
- **Author and Version**
- **Status** (Enabled/Disabled)
- **Installation Date**

#### Mod Actions
- **Select a mod** to see details and available actions
- **✅ Enable** - Activates a disabled mod
- **❌ Disable** - Deactivates an enabled mod (keeps files)
- **🗑️ Uninstall** - Removes the mod completely

### Mod Status Indicators

- **Green Circle** - Mod is enabled and active
- **Orange Circle** - Mod is disabled but installed
- **Gray Circle** - Mod status unknown

## Settings

Access settings via the **"⚙️ Settings"** button in the header:

### General Settings
- **Auto-check for updates** - Check for Lacesong updates on startup
- **Create backups before install** - Automatic backups before mod installations
- **Show advanced options** - Display additional configuration options
- **Enable telemetry** - Help improve Lacesong (optional)

### BepInEx Settings
- **Default Version** - Set the default BepInEx version for new installations

### Logging Settings
- **Log Level** - Control the verbosity of log messages
- **Open Logs Folder** - Access log files directly
- **Clear Logs** - Remove old log files

### Update Settings
- **Check for Updates** - Manually check for Lacesong updates
- **Last Checked** - Shows when updates were last checked

### Settings Management
- **Export Settings** - Save your configuration to a file
- **Import Settings** - Load settings from a file
- **Reset to Defaults** - Restore all settings to default values

## Logging and Troubleshooting

### Accessing Logs

1. Click **"📁 Open Logs"** in the header
2. Or go to Settings → Logging → **"📁 Open Logs Folder"**

### Log Files

- **Location**: `%APPDATA%\Lacesong\Logs\`
- **Format**: `lacesong-YYYY-MM-DD.log`
- **Content**: Detailed operation logs, errors, and debug information

### Common Issues

#### Game Not Detected
- **Solution**: Use manual selection to browse for your game folder
- **Check**: Ensure `Hollow Knight Silksong.exe` exists in the folder

#### BepInEx Installation Failed
- **Check**: Ensure you have write permissions to the game directory
- **Solution**: Run Lacesong as Administrator
- **Verify**: Game is not running during installation

#### Mod Installation Failed
- **Check**: Mod file is not corrupted
- **Verify**: Mod is compatible with current game version
- **Solution**: Check logs for detailed error information

#### Mod Not Working
- **Verify**: Mod is enabled in the Mod Catalog
- **Check**: BepInEx is properly installed
- **Solution**: Check game logs in `BepInEx\LogOutput.log`


## File Structure

After installation, your game directory will contain:

```
Hollow Knight Silksong/
├── Hollow Knight Silksong.exe
├── BepInEx/
│   ├── core/
│   │   ├── BepInEx.Core.dll
│   │   └── BepInEx.dll
│   ├── plugins/
│   │   └── [installed mods]
│   ├── config/
│   │   └── BepInEx.cfg
│   ├── logs/
│   │   └── LogOutput.log
│   └── backups/
│       └── [backup files]
├── winhttp.dll
└── doorstop_config.ini
```

## Backup and Restore

### Automatic Backups
- Created before BepInEx installations
- Created before mod installations
- Stored in `BepInEx/backups/`

### Manual Backups
- Use **"📦 Mod Catalog"** → **"🔄 Refresh"** → **"💾 Create Backup"**

### Restoring Backups
- Restores entire BepInEx configuration

## Updates

### Automatic Updates
- Lacesong checks for updates on startup (if enabled)
- Update notifications appear in the status bar
- Click **"🔄 Check Updates"** to check manually

### Manual Updates
1. Download latest installer from GitHub Releases
2. Run installer to update
3. Settings and configurations are preserved

## Advanced Usage

### Mod Development
- Place mod `.dll` files in `BepInEx/plugins/`
- Use `manifest.json` for mod metadata
- Check BepInEx documentation for mod development

### Configuration
- Edit `BepInEx/config/BepInEx.cfg` for advanced settings
- Modify `doorstop_config.ini` for loader configuration
- Backup configurations before making changes

### Troubleshooting
- Enable debug logging in Settings
- Check both Lacesong logs and BepInEx logs
- Verify game compatibility with mod versions

## Support

### Getting Help
- Check the [GitHub Issues](https://github.com/YourOrg/Lacesong/issues) page
- Review log files for error details
- Ensure you're running the latest version

### Reporting Issues
When reporting issues, include:
- Lacesong version
- Game version
- Operating system
- Relevant log files
- Steps to reproduce the problem

### Contributing
- Fork the repository
- Create a feature branch
- Submit a pull request
- Follow the contributing guidelines

## License

Lacesong is released under the MIT License. See [LICENSE](LICENSE) for details.

## Disclaimer

Lacesong is not affiliated with Team Cherry or Hollow Knight: Silksong. It is a community-driven tool for modding enthusiasts. Use mods at your own risk.

---

*For more information, visit the [Lacesong GitHub Repository](https://github.com/YourOrg/Lacesong).*

# Lacesong - Mod Manager Usage Guide

## Overview

Lacesong is a cross-platform mod management tool for Unity/Mono games, starting with Hollow Knight: Silksong. It provides a simple, safe, and consistent way to install, manage, and update community mods using the BepInEx mod loader.

## Installation

### Prerequisites

- Windows 10/11 (64-bit) or macOS 10.15+
- .NET 9.0 Runtime (included in installer)
- Hollow Knight: Silksong installed

### Download and Install

1. Download the latest Lacesong installer from [GitHub Releases](https://github.com/pecan-solutions/lacesong/releases)
   - Windows: Download the `.exe` installer
   - macOS: Download the `.dmg` installer
2. Run the installer and follow the setup wizard
3. Launch Lacesong from the Start Menu (Windows), Applications folder (macOS), or Desktop shortcut

## Getting Started

### First Launch

When you first launch Lacesong, it will automatically:

1. **Detect your game installation** - Scans for Hollow Knight: Silksong
2. **Check for updates** - Verifies you have the latest version
3. **Navigate to Home** - Shows the main dashboard

## Main Interface

### Navigation Sidebar

The left sidebar provides quick access to all major features:

- **Home** - Main dashboard with quick actions and game status
- **Game Detection** - Locate and configure your game installation
- **Browse Mods** - Discover and install new mods from the community
- **Installed Mods** - View and manage your currently installed mods
- **BepInEx Install** - Install and configure the BepInEx mod loader
- **Manage Mods** - Advanced mod management and configuration
- **Settings** - Application settings and preferences

### Bottom Bar

The persistent bottom bar provides:

- **Launch Controls** - Launch the game in modded or vanilla mode
- **Game Status** - Shows current game state and launch options
- **Split-button Design** - Main launch button with dropdown for mode selection

## Game Detection

### Automatic Detection

Lacesong automatically scans for Hollow Knight: Silksong installations:

- **Steam Library folders** - Common Steam installation paths
- **GOG Galaxy** - GOG Galaxy installations
- **Xbox Game Pass** - Xbox Game Pass installations
- **Common installation paths** - Standard installation directories

### Manual Selection

If automatic detection fails:

1. Navigate to **Game Detection** in the sidebar
2. Click **"Browse for Game"**
3. Navigate to your game folder containing the game executable
   - Windows: `Hollow Knight Silksong.exe`
   - macOS: `Hollow Knight Silksong.app`
4. Select the folder and click **"Continue"**

### Game Status

Once detected, the game status will show:
- Game name and installation path
- Detection status and configuration options
- Quick access to re-detect or change installation

## BepInEx Installation

Before installing mods, you need BepInEx (the mod loader):

### Installation Process

1. **Navigate to BepInEx Install** in the sidebar
2. **Check Status** - The screen shows if BepInEx is already installed
3. **Configure Options**:
   - **Version**: Automatically selects the latest compatible version
   - **Force Reinstall**: Check if you want to reinstall over existing installation
   - **Create Backup**: Recommended - backs up existing installation
4. **Install** - Click **"Install BepInEx"**
5. **Wait for Completion** - Installation progress is shown in the status area

### After Installation

- BepInEx files are installed to your game directory
- A `BepInEx` folder is created with the mod loader
- Configuration files are set up automatically
- You can now install and manage mods

## Mod Management

### Browse Mods

The **Browse Mods** section provides:

- **Search functionality** - Find mods by name, author, or description
- **Category filtering** - Filter by mod categories
- **Sorting options** - Sort by popularity, date, name, etc.
- **Mod details** - View descriptions, screenshots, and installation info
- **Quick install** - Install mods directly from the browser

### Installing Mods

#### From Browse Mods
1. Navigate to **Browse Mods**
2. Search or browse for desired mods
3. Click on a mod to view details
4. Click **"Install"** to download and install

#### From Home Dashboard
1. On the **Home** screen, use quick actions:
   - **"Install Mod from File"** - Select a local `.zip` file
   - **"Install Mod from URL"** - Enter a download URL

### Managing Installed Mods

The **Installed Mods** section shows:

- **All installed mods** with status indicators
- **Mod information** - Name, author, version, installation date
- **Status indicators**:
  - **Green Circle** - Mod is enabled and active
  - **Orange Circle** - Mod is disabled but installed
  - **Gray Circle** - Mod status unknown

#### Mod Actions
- **Select a mod** to see details and available actions
- **Enable** - Activates a disabled mod
- **Disable** - Deactivates an enabled mod (keeps files)
- **Uninstall** - Removes the mod completely
- **Update** - Check for and install updates

### Advanced Mod Management

The **Manage Mods** section provides:

- **Bulk operations** - Enable/disable multiple mods
- **Dependency management** - Handle mod dependencies
- **Conflict resolution** - Detect and resolve mod conflicts
- **Backup and restore** - Create and restore mod configurations

## Game Launching

### Launch Modes

Use the bottom bar launch controls:

- **Launch Modded** - Launches the game with all enabled mods
- **Launch Vanilla** - Launches the game without any mods
- **Stop Game** - Stops the currently running game

### Launch Process

1. **Select launch mode** using the split-button dropdown
2. **Click the main launch button** to start the game
3. **Monitor game status** in the bottom bar
4. **Use "Stop" button** to terminate the game if needed

## Settings

Access settings via the **Settings** button in the sidebar:

### General Settings
- **Auto-check for updates** - Check for Lacesong updates on startup
- **Create backups before install** - Automatic backups before mod installations
- **Show advanced options** - Display additional configuration options

### BepInEx Settings
- **Default Version** - Set the default BepInEx version for new installations
- **Update checking** - Configure automatic BepInEx update checking


### Update Settings
- **Check for Updates** - Manually check for Lacesong updates
- **Last Checked** - Shows when updates were last checked

### Settings Management
- **Export Settings** - Save your configuration to a file
- **Import Settings** - Load settings from a file
- **Reset to Defaults** - Restore all settings to default values


### Common Issues

#### Game Not Detected
- **Solution**: Use manual selection in **Game Detection**
- **Check**: Ensure the game executable exists in the folder
- **Verify**: Check file permissions and antivirus settings

#### BepInEx Installation Failed
- **Check**: Ensure you have write permissions to the game directory
- **Solution**: Run Lacesong as Administrator (Windows) or with elevated privileges (macOS)
- **Verify**: Game is not running during installation
- **Check**: Available disk space and antivirus interference

#### Mod Installation Failed
- **Check**: Mod file is not corrupted
- **Verify**: Mod is compatible with current game version
- **Solution**: Check logs for detailed error information
- **Try**: Download mod again or from different source

#### Mod Not Working
- **Verify**: Mod is enabled in **Installed Mods**
- **Check**: BepInEx is properly installed via **BepInEx Install**
- **Solution**: Check game logs in `BepInEx\LogOutput.log`
- **Try**: Disable other mods to check for conflicts

#### Launch Issues
- **Check**: Game executable exists and is not corrupted
- **Verify**: No antivirus blocking the game
- **Solution**: Try launching vanilla mode first
- **Check**: File permissions and path length limitations

## File Structure

After installation, your game directory will contain:

```
Hollow Knight Silksong/
├── Hollow Knight Silksong.exe (Windows) or Hollow Knight Silksong.app (macOS)
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
├── winhttp.dll (Windows only)
└── doorstop_config.ini
```

## Backup and Restore

### Automatic Backups
- Created before BepInEx installations
- Created before mod installations
- Stored in `BepInEx/backups/`

### Manual Backups
- Use **Manage Mods** → **"Create Backup"**
- Export settings via **Settings**

### Restoring Backups
- Restores entire BepInEx configuration
- Can restore individual mod configurations
- Settings can be imported from exported files

## Updates

### Automatic Updates
- Lacesong checks for updates on startup (if enabled)
- Update notifications appear in the status bar
- Click **"Check Updates"** in Settings to check manually

### Manual Updates
1. Download latest installer from [GitHub Releases](https://github.com/pecan-solutions/lacesong/releases)
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
- Verify game compatibility with mod versions
- Use **Manage Mods** for conflict detection

## Support

### Getting Help
- Check the [GitHub Issues](https://github.com/pecan-solutions/lacesong/issues) page
- Review log files for error details
- Ensure you're running the latest version

### Reporting Issues
When reporting issues, include:
- Lacesong version
- Game version
- Operating system and version
- Relevant log files
- Steps to reproduce the problem

## License

Lacesong is released under the MIT License. See [LICENSE](LICENSE) for details.

## Disclaimer

Lacesong is not affiliated with Team Cherry or Hollow Knight: Silksong. It is a community-driven tool for modding enthusiasts. Use mods at your own risk.

---

*For more information, visit the [Lacesong GitHub Repository](https://github.com/pecan-solutions/lacesong).*
# Lacesong

A cross-platform mod management and loader tool for Unity/Mono games (starting with Hollow Knight: Silksong).

Lacesong provides players with a simple, safe, and consistent way to install, manage, and update community mods. Inspired by prior tools like Lumafly and Scarab, Lacesong focuses on automation, transparency, and cross-platform compatibility, eliminating the need for manual file editing or fragile patching processes.

## Features

### Core Functionality
- **Modern WPF Interface** - Clean, intuitive dark-themed UI with MVVM architecture
- **Automatic Game Detection** - Finds Hollow Knight Silksong installations automatically via Steam/Epic/GoG registry scanning or manual selection
- **BepInEx Management** - One-click BepInEx installation with version selection and configuration
- **Comprehensive Mod Management** - Install mods from files or URLs with dependency resolution
- **Built-in Logging System** - Comprehensive logging with easy access to log files
- **Update Management** - Built-in updater that checks GitHub releases automatically
- **Settings & Configuration** - Extensive settings management with import/export functionality

### Advanced Features
- **Automatic Mod Updates** - Opt-in automatic updates with smart filtering and config preservation
- **Conflict Detection** - Advanced conflict detection and resolution system
- **Configuration Preservation** - Intelligent config merging during updates
- **Compatibility Checking** - Game version and dependency compatibility validation
- **Enhanced Backup System** - Restore points with metadata tracking
- **Mod Index Integration** - Browse and install mods from centralized repositories
- **Dependency Resolution** - Automatic dependency installation and conflict resolution
- **Signature Verification** - Cryptographic verification of mod files and downloads
- **Safe Installation Staging** - Atomic operations with comprehensive validation

## Tech Stack

### Core
- **.NET 9 / C#** - Modern, high-performance backend logic for mod discovery, manifest parsing, dependency resolution
- **BepInEx** - Unity/Mono mod loader integration layer with comprehensive management
- **JSON** - Mod manifests, dependency descriptors, and configuration management

### WPF Frontend
- **WPF with MVVM** - Modern Windows UI using CommunityToolkit.Mvvm for clean architecture
- **Dark Theme** - Custom styling with modern design principles and accessibility
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection for service management
- **Logging** - Microsoft.Extensions.Logging with file and console providers

### Services & Integration
- **Octokit** - GitHub API integration for update checking and release management
- **System.CommandLine** - Comprehensive CLI interface for advanced users
- **FlaUI** - UI automation testing framework for quality assurance

### Testing & Quality
- **xUnit** - Unit testing framework with comprehensive test coverage
- **Moq** - Mocking framework for isolated testing
- **FlaUI** - UI automation testing for user interaction validation

### Packaging & Distribution
- **Self-contained deployments** - Single-file executables with all dependencies
- **GitHub Releases** - Automated release management and distribution
- **Cross-platform builds** - Ready for macOS/Linux expansion

## Installation

### Windows (Current Support)

1. Download the latest Lacesong installer from [GitHub Releases](https://github.com/YourOrg/Lacesong/releases)
2. Run the installer - it will auto-detect your Silksong installation
3. Launch Lacesong from Start Menu or Desktop

### Command Line Interface

For advanced users, a CLI version is also available:

1. Download the CLI package from [Releases](https://github.com/YourOrg/Lacesong/releases)
2. Extract to a folder and run `lacesong.exe` from command line
3. See [USAGE.md](USAGE.md) for CLI commands and options

### macOS/Linux (Planned Support)

- macOS ARM64 + Intel builds packaged with `.dmg` installer
- Linux `.AppImage` or `.deb` for distribution

## Usage

### WPF Application

1. **Launch Lacesong** - The application will automatically detect your game installation
2. **Game Detection** - If not detected automatically, use the "Browse for Game" option
3. **Install BepInEx** - Navigate to "BepInEx Install" and click "Install BepInEx"
4. **Manage Mods** - Use the "Mod Catalog" to install, enable, disable, or uninstall mods
5. **Settings** - Configure preferences, check for updates, and manage logs

### Command Line Interface

```bash
# Detect game installation
lacesong detect-game

# Install BepInEx
lacesong install-bepinex --version 5.4.22 --backup

# Install a mod
lacesong install-mod "path/to/mod.zip"

# List installed mods
lacesong list-mods

# Enable/disable mod
lacesong enable-mod "mod-id"
lacesong disable-mod "mod-id"

# Create backup
lacesong backup "backup-name"

# Restore backup
lacesong restore "backup-file.lcb"

# Search mods from index
lacesong search-mods --category UI --verified true

# Check for updates
lacesong check-updates

# Resolve conflicts
lacesong detect-conflicts
```

### Advanced CLI Commands

```bash
# Mod index management
lacesong add-repo "my-repo" "My Custom Mods" "https://example.com/mods.json"
lacesong refresh-index
lacesong install-from-index "example-mod" --version "1.2.0"

# Safety features
lacesong verify-checksum "mod.zip" "abc123def456" --algorithm SHA256
lacesong check-permissions --path "C:\Program Files\Hollow Knight Silksong"
lacesong create-restore-point "before-update" --description "Before major update"

# Configuration management
lacesong backup-configs "mod-id"
lacesong restore-configs "mod-id"
```

## Architecture

### Service-Oriented Design
- Clear interfaces (`IGameDetector`, `IBepInExManager`, `IModManager`, `IBackupManager`, `IModUpdateService`, `IConflictDetectionService`)
- Dependency injection ready
- Testable and mockable components

### Data Models
- Comprehensive `GameInstallation` model
- Detailed `ModInfo` model with dependencies
- Flexible `BepInExInstallOptions` configuration
- Rich `OperationResult` for operation feedback
- Advanced models for updates, conflicts, and compatibility

### File Operations
- Safe temporary directory usage
- Atomic file operations
- Comprehensive error handling
- Cross-platform path handling

## Safety Features

### Dependency Resolution
- Automatic BepInEx version checking and mod dependency validation
- Support for exact, range, and tilde version constraints
- Missing dependency installation from repositories
- Conflict detection and resolution

### Signature & Checksum Verification
- SHA1, SHA256, SHA384, SHA512, MD5 support
- Cryptographic signature validation framework
- File integrity checks and corruption detection
- Permission validation

### Safe Installation Staging
- Install to temporary directory first
- Comprehensive validation pipeline
- Atomic operations with rollback support
- Executable validation and cleanup management

### Enhanced Backup System
- Restore points with rich metadata
- Automatic backup creation before operations
- Compressed backup archives
- Tag system for categorization

### User Permissions & Elevation
- Cross-platform permission detection
- Automatic elevation handling when needed
- Protected location detection
- Clear elevation reasons

## Advanced UX Features

### Automatic Mod Updates
- Opt-in automatic updates per mod
- Update channels (stable, beta, alpha)
- Scheduled background checking
- Smart filtering for non-breaking updates
- Configuration preservation during updates

### Conflict Detection System
- File conflicts (same DLL names)
- Dependency conflicts and version mismatches
- Load order conflicts and config overlaps
- Automatic resolution where possible
- Manual resolution for complex conflicts

### Configuration Management
- Intelligent config merging during updates
- Multi-format support (JSON, YAML, INI, XML, TOML)
- Automatic backup before changes
- User modification detection
- Easy restoration from backups

### Compatibility System
- Game version compatibility checking
- BepInEx version validation
- Dependency compatibility verification
- Known issues tracking
- Community-driven compatibility reports

## Mod Index & Catalog

### Centralized Mod Discovery
- Comprehensive JSON schema for mod metadata
- Support for multiple repositories
- Advanced search with filtering
- Pagination for large catalogs

### GitHub Integration
- Automatic mod discovery from GitHub repositories
- Asset detection and metadata extraction
- Version management and release tracking

### Admin Tools
- Curated index management
- Validation and publishing capabilities
- Repository management
- Automated hosting setup

## Testing Coverage

The test suite covers:
- Game detection functionality
- BepInEx installation and management
- Mod installation, uninstallation, enable/disable
- Backup creation and restoration
- Manifest parsing and serialization
- Error handling and operation results
- File operation safety
- Advanced safety features
- UI automation testing

## Build & Distribution

### Build Scripts
- `build.bat` - Windows build script
- `build.sh` - Unix-like systems build script
- Automated testing integration
- Self-contained executable creation

### Distribution Packages
- WPF Application package
- CLI package
- Cross-platform builds
- Automated release management

## Contributing

Contributions are welcome!

- Open issues for bug reports or feature requests
- Submit PRs for fixes or new features
- Follow CONTRIBUTING.md guidelines

## License

MIT License - free to use, modify, and distribute.

## Disclaimer

Lacesong is not affiliated with Team Cherry or Hollow Knight: Silksong. It is a community-driven tool for modding enthusiasts. Use mods at your own risk.

## Documentation

- **[USAGE.md](USAGE.md)** - Comprehensive usage guide with step-by-step instructions
- **Architecture Overview** - Technical documentation of the system design
- **API Reference** - Core library interfaces and services
- **Contributing Guidelines** - How to contribute to the project
- **Build Instructions** - How to build and develop Lacesong locally
# Changelog
All notable changes to this project will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- Add mod profiles for dynamic and quick mod loading

### Changed
- Adjust the launch options to be in a bar at the top.

## [0.4.0] - 2025-10-18
### Added
- Auto-update BepInEx functionality with automatic latest version detection
- Dynamic BepInEx version resolution based on target executable platform
- Cross-platform BepInEx installation support (Windows, macOS, Linux)
- Automatic Rosetta installation for macOS .app bundles
- Platform-specific executable detection and handling
- Enhanced BepInEx installation UI with simplified version selection
- Improved mod name formatting with better underscore and dash handling

### Changed
- Simplified BepInEx installation to automatically install latest version
- Enhanced BepInEx download system to use GitHub API for asset resolution
- Improved platform detection based on target executable type rather than current OS
- Streamlined BepInEx installation UI by removing version selection dropdown
- Enhanced mod update service interface with force check parameter

### Fixed
- Fixed BepInEx installation for macOS .app bundles with proper executable name detection
- Improved cross-platform compatibility for BepInEx installation scripts
- Enhanced mod name display with better formatting for underscores and dashes

## [0.3.0] - 2025-10-18
### Added
- Mod update checking functionality with Thunderstore API integration
- "Check for Mod Updates" button in settings view
- ThunderstorePackageDetailDto model for package detail API responses
- Comprehensive mod update service with caching support
- Disk and memory caching for Thunderstore API calls
- Unit tests for mod update functionality
- Automatic startup update checks when game is detected
- Scheduled update checks for continuous mod monitoring

### Changed
- Enhanced GameStateService to trigger update checks on game detection
- Updated SettingsViewModel to include mod update checking capabilities
- Improved ModUpdateService to use direct Thunderstore API instead of local index
- Enhanced ThunderstoreService with package detail API support
- Platform depends on targeted executable, not the platform itself.

### Fixed
- Improved mod update detection accuracy using latest Thunderstore data
- Better error handling for mod update operations

## [0.2.0] - 2025-10-18
### Added
- Quick uninstall and install mods from the list view on BrowseMods
- ModInstallStatusConverter and ModNotInstallStatusConverter for dynamic button visibility
- Efficient installed mod tracking using HashSet for fast lookups
- Enhanced mod installation with owner information from Thunderstore API
- Comprehensive uninstall functionality with confirmation dialogs
- Cross-platform symbolic link support for mod files and directories
- Directory junction creation for Windows systems
- Recursive directory copying fallback for unsupported systems
- Enhanced mod analysis with better manifest parsing and fallback handling
- Unit tests for symlink creation and directory mirroring functionality

## [0.1.0] - 2025-10-17
### Added
- Enhanced launch mode management with dynamic button states (Launch/Stop)
- Comprehensive process monitoring and management system
- Cross-platform game launcher improvements (Windows, macOS, Linux)
- Support for `run_bepinex.sh` script on non-Windows systems
- macOS `.app` bundle detection and handling
- Graceful game shutdown with fallback to force kill
- Process tracking and cleanup with proper resource management
- Comprehensive unit tests for GameLauncher functionality
- Enhanced debugging and logging throughout launch process

### Changed
- Improved command execution logic with separate start/stop capabilities
- Enhanced UI responsiveness with dynamic button text based on game state
- Better error handling and exception reporting in game launcher
- Refactored HomeViewModel to implement IDisposable for proper cleanup

### Fixed
- Prevent double-launch scenarios and deadlock in game startup
- Resolve compilation errors with await statements in lock contexts
- Improve thread safety in process management operations

## [0.0.1] - 2025-10-17
### Added
- CHANGELOG.md tracking
- Image assets organization with dedicated images directory
- Documentation structure improvements

### Changed
- Reorganized documentation assets (banner and screenshot images moved to docs/images/)
- Updated README.md with improved content structure

### Removed
- Removed obsolete bepinex-platform-paths.md documentation
- Removed placeholderimage.png asset

### Fixed
- Enhanced dialog service functionality
- Improved UI controls styling
- Updated mod management view model logic
- Refined manage mods view interface


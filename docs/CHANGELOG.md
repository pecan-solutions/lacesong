# Changelog
All notable changes to this project will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- Profile management

## [1.0.0] - 2025-01-16
### Added
- **Complete GitHub issue management system** with comprehensive issue templates
  - Bug report template with detailed environment information collection
  - Feature request template with implementation considerations and priority assessment
  - Specialized mod management issue template for mod-related problems
  - Issue configuration with community support links and documentation references
- **Enhanced project documentation and metadata**
  - Updated manifest.json with complete project information and feature specifications
  - Comprehensive usage documentation with detailed installation and feature guides
  - Updated README.md with improved project description and contribution guidelines
  - Updated LICENSE file with proper Apache 2.0 license terms
- **Robust application architecture improvements**
  - Enhanced service layer with improved dependency injection patterns
  - Refactored ViewModels with better separation of concerns
  - Improved dialog service implementation with modal overlay support
  - Enhanced settings management with persistent configuration
- **Advanced mod management capabilities**
  - Comprehensive mod installation and uninstallation workflows
  - Enhanced mod browsing with Thunderstore integration
  - Improved mod update detection and management
  - Better conflict detection and dependency resolution
- **Cross-platform game launcher enhancements**
  - Improved game detection across Windows, macOS, and Linux
  - Enhanced BepInEx installation and management
  - Better process monitoring and game state tracking
  - Streamlined launch controls with persistent bottom bar
- **Core service layer improvements**
  - Enhanced backup management with configurable retention policies
  - Improved platform detection and compatibility services
  - Better verification and permission handling
  - Optimized mod indexing and caching services
- **Lacesong version checking and update system**
  - New LacesongVersionService for checking application updates via GitHub API
  - Automatic version checking on game detection with user notifications
  - Enhanced update information display in settings view
  - Background update checking with proper error handling
  - Version comparison logic for determining update availability
  - Integration with existing notification system for update alerts

### Changed
- **Application version updated to 1.0.0** marking the first stable release
- **Improved error handling and user feedback** throughout the application
- **Enhanced UI consistency** with better styling and user experience
- **Streamlined service architecture** with better separation of concerns
- **Updated project metadata** to reflect mature, production-ready status

### Fixed
- **Resolved various UI inconsistencies** and improved overall user experience
- **Enhanced mod management reliability** with better error handling
- **Improved cross-platform compatibility** for game detection and mod installation
- **Better resource management** and cleanup throughout the application

### Removed
- **Removed LoggingService.cs** as part of service layer refactoring
- **Cleaned up obsolete code** and improved code organization

## [0.7.2] - 2025-10-19
### Added
- BepInEx version caching service to reduce API calls and improve performance
- Input dialog implementation with modal overlay and URL input functionality
- Version refresh functionality for BepInEx installation view

### Changed
- Refactored BepInEx version fetching to use centralized caching service
- Improved dialog service with proper modal implementation
- Enhanced BepInEx installation view model with dependency injection

### Fixed
- Reduced unnecessary GitHub API calls for BepInEx version checking
- Improved application performance with cached version information

## [0.7.1] - 2025-10-19
### Added
- Bottom bar with persistent launch controls and logo branding
- Split-button design for launch mode selection (Modded/Vanilla)
- Preferred launch mode setting with persistence
- Enhanced game state monitoring with process tracking
- Dynamic launch button states (Launch/Stop) with contextual text

### Changed
- Move launch functionality from HomeView to persistent bottom bar
- Update MainWindow layout to use DockPanel with bottom bar
- Improve launch button states with dynamic text based on game status

### Fixed
- Enhanced UI consistency with branded bottom bar
- Better launch control accessibility with persistent placement

## [0.7.0] - 2025-10-19
### Removed
- Removed CLI functionality from the application
- Simplified application architecture by removing command-line interface

### Changed
- Streamlined application to focus on GUI-only experience
- Reduced application complexity and maintenance overhead

## [0.6.0] - 2025-10-19
### Added
- CLI command for BepInEx backup cleanup with configurable retention policies
- Support for max-backups and max-age-days parameters in backup cleanup
- Enhanced mod display names with better formatting (underscores replaced with spaces)
- Improved mod loading with comprehensive debug output
- Better progress reporting for BepInEx operations

### Changed
- Enhanced mod management reliability with improved error handling
- Updated UI to use DisplayName property for consistent mod name formatting
- Improved mod enable/disable operations with better directory name handling
- Adjusted progress bar width for better visual consistency

### Fixed
- Improved mod management reliability and debug logging
- Enhanced mod loading performance with better error reporting
- Fixed mod enable/disable operations to use proper directory names

## [0.5.0] - 2025-10-19
### Added
- Comprehensive BepInEx update checking and management functionality
- GitHub API integration for automatic latest release detection
- BepInExUpdate, BepInExUpdateOptions, and BackupCleanupResult models
- Automatic update checking with progress reporting
- Backup cleanup functionality with configurable retention policies
- Version comparison and asset URL resolution for updates
- Enhanced backup creation with fallback to temp directory
- Cross-platform BepInEx update support

### Changed
- Extended IBepInExManager interface with update and cleanup methods
- Enhanced BepInEx installation process with better backup handling
- Improved backup creation to handle permission issues gracefully
- Updated mod management to use DisplayName property for better UI consistency

### Fixed
- Enhanced BepInEx installation reliability with better error handling
- Improved backup creation to handle directory permission issues
- Better cleanup of temporary files during BepInEx operations

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


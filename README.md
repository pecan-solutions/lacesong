# Lacesong

![Lacesong Banner](docs/lacesongbanner.png)

A cross-platform mod management tool for Unity/Mono games, starting with Hollow Knight: Silksong. Built with .NET 9 and Avalonia UI, Lacesong runs natively on Windows, macOS, and Linux.

## Overview

Lacesong provides a simple, safe, and consistent way to install, manage, and update community mods. Inspired by tools like Lumafly and Scarab, it focuses on automation, safety, and cross-platform compatibility. Looking for contributors or developers.

## Screenshots

![Application Screenshot of the Mod Catalog](docs/screenshotlacesong.png)
Mod catalog screenshot showing Thunderstore integration and "The Marrow" theme.

## Tech Stack

**Core**: .NET 9, C#, BepInEx integration, JSON-based manifests  
**UI**: Avalonia (cross-platform MVVM), CommunityToolkit.Mvvm  
**Services**: Octokit (GitHub API), System.CommandLine (CLI)  
**Testing**: xUnit, Moq, FlaUI (UI automation)  
**Distribution**: Self-contained executables for Windows, macOS, Linux

## Installation

### Desktop Application

Desktop application is work in progress and needs to be tested on Linux first.

## Development

### Build System

Build scripts for all platforms:
```bash
# Windows
build.bat

# macOS/Linux
./build.sh
```

Produces self-contained executables with all dependencies included.

## Contributing

We welcome contributions! Whether you're fixing bugs, adding features, or improving documentation, your help is appreciated.

Please read our [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Development environment setup
- Code style guidelines and conventions
- Testing requirements
- Pull request process
- Commit message guidelines

Quick start: Open issues for bugs or features, submit PRs for improvements. All contributions should include tests and follow existing code style.

## License

MIT License - See [LICENSE](LICENSE) for details.

## Disclaimer

Lacesong is a community-driven tool, not affiliated with Team Cherry or Hollow Knight: Silksong. Use at your own risk.

## Credits

- [wdarrenww](https://github.com/wdarrenw) (Darren Wei) - Creator of Lacesong, Lead Developer, Planner/PM

- lavenderpres (Presley) - Assistant Planning/PM, Senior Developer, Avalonia Translation

- piespecan (Iris) - Senior Developer, Avalonia and WPF UI Development, Avalonia Translation

- Leonardo - Developer, Planning, WPF development

- Joseph - Developer, Avalonia UI

- Sylvie - Developer, Avalonia UI and Mod Logic

Thanks in advance to all future contributors!

---

**Documentation**: See [USAGE.md](USAGE.md) for detailed usage instructions.

**Contact**: wwdarrenwei@gmail.com (email) or stitchsages (discord)

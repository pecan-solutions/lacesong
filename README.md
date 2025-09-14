# **Lacesong**

*A cross-platform mod management and loader tool for Unity/Mono games (starting with Hollow Knight: Silksong).*

Lacesong provides players with a simple, safe, and consistent way to install, manage, and update community mods. Inspired by prior tools like **Lumafly** and **Scarab**, Lacesong focuses on **automation, transparency, and cross-platform compatibility**, eliminating the need for manual file editing or fragile patching processes.

---

## **✨ Features**

* **Modern WPF Interface**  
   Clean, intuitive dark-themed UI with MVVM architecture.  
   Responsive design with comprehensive navigation and status indicators.

* **Automatic Game Detection**  
   Finds Hollow Knight Silksong installations automatically via Steam/Epic/GoG registry scanning or manual selection.  
   Supports multiple installation sources and validates game integrity.

* **BepInEx Management**  
   One-click BepInEx installation with version selection and configuration.  
   Automatic backup creation and safe uninstallation procedures.

* **Comprehensive Mod Management**  
   Install mods from files or URLs with dependency resolution.  
   Enable/disable mods without uninstalling, safe uninstallation with backups.

* **Built-in Logging System**  
   Comprehensive logging with easy access to log files.  
   Multiple log levels and automatic log rotation.

* **Update Management**  
   Built-in updater that checks GitHub releases automatically.  
   Safe update process with rollback capabilities.

* **Settings & Configuration**  
   Extensive settings management with import/export functionality.  
   Customizable preferences and advanced options.

* **Cross-Platform Foundation**  
   ✅ Windows (WPF frontend with .NET 9).  
   🔜 macOS/Linux support (via Avalonia UI in Phase 3).

---

## **🛠️ Tech Stack**

### **Core**

* **.NET 9 / C\#** – Modern, high-performance backend logic for mod discovery, manifest parsing, dependency resolution.

* **BepInEx** – Unity/Mono mod loader integration layer with comprehensive management.

* **JSON** – Mod manifests, dependency descriptors, and configuration management.

### **WPF Frontend (Phase 2)**

* **WPF with MVVM** – Modern Windows UI using CommunityToolkit.Mvvm for clean architecture.

* **Dark Theme** – Custom styling with modern design principles and accessibility.

* **Dependency Injection** – Microsoft.Extensions.DependencyInjection for service management.

* **Logging** – Microsoft.Extensions.Logging with file and console providers.

### **Services & Integration**

* **Octokit** – GitHub API integration for update checking and release management.

* **System.CommandLine** – Comprehensive CLI interface for advanced users.

* **FlaUI** – UI automation testing framework for quality assurance.

### **Testing & Quality**

* **xUnit** – Unit testing framework with comprehensive test coverage.

* **Moq** – Mocking framework for isolated testing.

* **FlaUI** – UI automation testing for user interaction validation.

### **Packaging & Distribution**

* **Self-contained deployments** – Single-file executables with all dependencies.

* **GitHub Releases** – Automated release management and distribution.

* **Cross-platform builds** – Ready for macOS/Linux expansion in Phase 3.

---

## **📦 Installation**

### **Windows (Current Support)**

1. Download the latest Lacesong installer from [GitHub Releases](https://github.com/YourOrg/Lacesong/releases).

2. Run the installer – it will auto-detect your Silksong installation.

3. Launch `Lacesong` from Start Menu or Desktop.

### **Command Line Interface**

For advanced users, a CLI version is also available:

1. Download the CLI package from [Releases](https://github.com/YourOrg/Lacesong/releases).

2. Extract to a folder and run `lacesong.exe` from command line.

3. See [USAGE.md](USAGE.md) for CLI commands and options.

### **macOS/Linux (Planned Support)**

* macOS ARM64 \+ Intel builds packaged with `.dmg` installer.

* Linux `.AppImage` or `.deb` for distribution.

---

## **🚀 Usage**

### **WPF Application**

1. **Launch Lacesong** – The application will automatically detect your game installation.

2. **Game Detection** – If not detected automatically, use the "Browse for Game" option.

3. **Install BepInEx** – Navigate to "BepInEx Install" and click "Install BepInEx".

4. **Manage Mods** – Use the "Mod Catalog" to install, enable, disable, or uninstall mods.

5. **Settings** – Configure preferences, check for updates, and manage logs.

### **Command Line Interface**

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
```

### **Detailed Documentation**

For comprehensive usage instructions, troubleshooting, and advanced features, see [USAGE.md](USAGE.md).

---

## **🧑‍💻 Development Roadmap**

### **Phase 1 – Core Foundation** ✅

* Core library: manifest parsing, mod installation, dependency resolution.
* CLI tool with comprehensive command set.
* Comprehensive unit tests (21 test cases).
* Cross-platform build system.

### **Phase 2 – WPF Application** ✅

* **WPF frontend with MVVM architecture**.
* **Modern dark theme UI** with intuitive navigation.
* **Game detection screen** with automatic and manual detection.
* **BepInEx installation flow** with version management.
* **Mod catalog view** with install/uninstall/enable/disable functionality.
* **Settings management** with preferences and configuration.
* **Local logging system** with "Open logs" action.
* **Built-in updater** that checks GitHub releases.
* **UI automation tests** for user interactions.
* **Comprehensive documentation** (USAGE.md).

### **Phase 3 – Cross-Platform Expansion**

* Port frontend to Avalonia UI.
* macOS ARM64/Intel builds with `.dmg` packaging.
* Linux builds (AppImage/Deb).

### **Phase 4 – Advanced Features**

* Cloud sync of profiles.
* Mod registry API (community-driven).
* Plugin SDK for custom mod manager extensions.

---

## **📖 Documentation**

* **[USAGE.md](USAGE.md)** – Comprehensive usage guide with step-by-step instructions
* **Architecture Overview** – Technical documentation of the system design
* **API Reference** – Core library interfaces and services
* **Contributing Guidelines** – How to contribute to the project
* **Build Instructions** – How to build and develop Lacesong locally

---

## **🤝 Contributing**

Contributions are welcome\!

* Open issues for bug reports or feature requests.

* Submit PRs for fixes or new features.

* Follow CONTRIBUTING.md.

---

## **📜 License**

MIT License – free to use, modify, and distribute.

---

## **⚠️ Disclaimer**

Lacesong is **not affiliated with Team Cherry** or Hollow Knight: Silksong.  
 It is a community-driven tool for modding enthusiasts. Use mods at your own risk.

# **Lacesong**

*A cross-platform mod management and loader tool for Unity/Mono games (starting with Hollow Knight: Silksong).*

Lacesong provides players with a simple, safe, and consistent way to install, manage, and update community mods. Inspired by prior tools like **Lumafly** and **Scarab**, Lacesong focuses on **automation, transparency, and cross-platform compatibility**, eliminating the need for manual file editing or fragile patching processes.

---

## **✨ Features**

* **Automatic Mod Installation**  
   Drag-and-drop or one-click install mods packaged as `.zip` or `.dll`.  
   Automatically resolves directory placement and ensures compatibility with BepInEx.

* **Dependency Management**  
   Detects required frameworks (e.g., Satchel, WeaverCore) and installs missing dependencies automatically.

* **Game Detection**  
   Finds Hollow Knight Silksong installation directories automatically via Steam/Epic/GoG registry scanning or manual selection.

* **Safe Mod Updating**  
   Uses a remote repository (JSON manifest / API-driven) to check for new versions and update installed mods without breaking configs.

* **Profiles and Presets (Planned)**  
   Save multiple mod configurations for different playthroughs or testing scenarios.

* **Cross-Platform**  
   ✅ Windows (primary target, .NET 8 WPF/WinUI frontend).  
   🔜 macOS/Linux support (via Avalonia UI \+ Mono backend).

---

## **🛠️ Tech Stack**

### **Core**

* **.NET 8 / C\#** – Shared backend logic for mod discovery, manifest parsing, dependency graph building.

* **BepInEx** – Unity/Mono mod loader integration layer.

* **YAML/JSON** – Mod manifests, dependency descriptors, and repository indexing.

### **Windows Frontend**

* **WPF or WinUI 3** – Native Windows UI for v1 release.

* **Squirrel.Windows** – Auto-updater and installer for easy distribution.

### **Cross-Platform Frontend (v2)**

* **Avalonia UI** – Enables a single shared UI across Windows/macOS/Linux.

* **MAUI (optional)** – If mobile clients (remote mod browsing) become desirable.

### **Packaging & Distribution**

* **NuGet** – For internal libraries.

* **GitHub Releases** – Hosting installer builds.

* **Future: Custom Mod API / Registry** – For community mod hosting, searchable from inside the manager.

---

## **📦 Installation**

### **Windows (Current Support)**

1. Download the latest `.exe` installer from [Releases](https://github.com/YourOrg/HollowModManager/releases).

2. Run the installer – it will auto-detect your Silksong installation.

3. Launch `Lacesong` from Start Menu or Desktop.

### **macOS/Linux (Planned Support)**

* macOS ARM64 \+ Intel builds packaged with `.dmg` installer.

* Linux `.AppImage` or `.deb` for distribution.

---

## **🚀 Usage**

1. **Install BepInEx** (if not already bundled). Lacesong will attempt to detect and install the correct BepInEx version.

2. **Browse & Install Mods**

   * Drag a `.zip` into the app.

   * Use the built-in mod browser (if configured to a registry API).

3. **Enable / Disable Mods**  
    Toggle mods per profile. Disabled mods are safely unloaded without uninstalling files.

4. **Update Mods**  
    Click “Check for Updates” to see which mods are out of date.

5. **Profiles (Planned)**  
    Save/load sets of mods for speedrunning, casual playthroughs, or testing.

---

## **🧑‍💻 Development Roadmap**

### **Phase 1 – Windows MVP**

* Core library: manifest parsing, mod installation, dependency resolution.

* WPF frontend with mod list UI.

* BepInEx auto-installer.

* Profile saving/loading.

* Initial GitHub release.

### **Phase 2 – Cross-Platform Expansion**

* Port frontend to Avalonia UI.

* macOS ARM64/Intel builds with `.dmg` packaging.

* Linux builds (AppImage/Deb).

### **Phase 3 – Advanced Features**

* Cloud sync of profiles.

* Mod registry API (community-driven).

* Plugin SDK for custom mod manager extensions.

---

## **📖 Documentation**

* Architecture Overview

* How Mod Installation Works

* Contributing Guidelines

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

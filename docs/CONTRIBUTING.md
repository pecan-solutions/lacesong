# Contributing to Lacesong

Thank you for your interest in contributing to Lacesong! This document provides guidelines and information for contributors.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Environment Setup](#development-environment-setup)
- [Project Structure](#project-structure)
- [Code Style Guidelines](#code-style-guidelines)
- [Testing](#testing)
- [Commit Guidelines](#commit-guidelines)
- [Pull Request Process](#pull-request-process)
- [Issue Reporting](#issue-reporting)
- [Architecture Overview](#architecture-overview)

## Code of Conduct

We are committed to providing a welcoming and inclusive environment. Please be respectful and professional in all interactions.

### Our Standards

- Be respectful and considerate
- Welcome newcomers and help them learn
- Focus on constructive feedback
- Accept responsibility for mistakes
- Prioritize what's best for the project and community

## Getting Started

### Ways to Contribute

- Report bugs or suggest features via [GitHub Issues](https://github.com/pecansolutions/lacesong/issues)
- Improve documentation
- Fix bugs or implement new features
- Write or improve tests
- Review pull requests

### Prerequisites

- .NET 9.0 SDK or later
- Git for version control
- IDE with C# support (Visual Studio, VS Code, or JetBrains Rider)
- Basic knowledge of C# and MVVM patterns

## Development Environment Setup

### 1. Fork and Clone

```bash
# fork the repository on github first, then:
git clone https://github.com/YOUR-USERNAME/lacesong.git
cd lacesong
```

### 2. Build the Project

**Windows:**
```bash
build.bat
```

**macOS/Linux:**
```bash
./build.sh
```

For development builds (faster):
```bash
# windows
dev-build.bat

# macos/linux
./dev-build.sh
```

### 3. Run Tests

```bash
dotnet test
```

### 4. Project Structure

```
lacesong/
├── src/
│   ├── Lacesong.Core/          # core business logic, platform-agnostic
│   ├── Lacesong.CLI/           # command-line interface
│   ├── Lacesong.Avalonia/      # cross-platform ui (primary)
│   └── Lacesong.WPF/           # windows-specific ui (legacy)
├── tests/
│   ├── Lacesong.Tests/         # core logic tests
│   └── Lacesong.WPF.Tests/     # ui tests
├── docs/                       # documentation
├── build/                      # build outputs
└── dist/                       # distribution files
```

## Code Style Guidelines

### General Principles

- Follow standard C# conventions and .NET best practices
- Write clean, readable, and maintainable code
- Keep methods focused and concise
- Use meaningful names for variables, methods, and classes
- Match the existing code style in the project

### Naming Conventions

```csharp
// classes, interfaces, methods, properties: PascalCase
public class BepInExManager
public interface IGameDetector
public async Task<OperationResult> InstallBepInEx()
public string InstallPath { get; set; }

// private fields: camelCase with underscore prefix
private readonly IGameDetector _gameDetector;
private string _detectionStatus;

// local variables, parameters: camelCase
var gameInstall = GetGameInstallation();
public void ProcessGame(string gamePath)

// constants: PascalCase
private const string BepInExDownloadUrlTemplate = "...";
```

### Code Comments

- Use comments sparingly, only where necessary
- All code comments should be in lowercase
- Comment style reflects an experienced senior developer teaching a junior
- Only comment:
  - Complex or convoluted logic
  - Important architectural decisions
  - Non-obvious implementation details
  - TODOs or areas for improvement
  
```csharp
// good: explains non-obvious platform-specific behavior
// on macos, bepinex must be installed in the folder containing the .app bundle
return Path.GetDirectoryName(gameInstall.InstallPath);

// bad: states the obvious
// get the game path
var path = game.InstallPath;
```

### XML Documentation

Use XML documentation for public APIs:

```csharp
/// <summary>
/// service for managing bepinex installation and configuration
/// </summary>
public class BepInExManager : IBepInExManager
{
    /// <summary>
    /// installs bepinex to the specified game installation
    /// </summary>
    /// <param name="gameInstall">the game installation to install to</param>
    /// <param name="options">installation options</param>
    /// <returns>operation result indicating success or failure</returns>
    public async Task<OperationResult> InstallBepInEx(
        GameInstallation gameInstall, 
        BepInExInstallOptions options)
    {
        // implementation
    }
}
```

### File Organization

```csharp
// 1. using statements (sorted)
using System;
using System.Linq;
using System.Threading.Tasks;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;

// 2. namespace (file-scoped preferred for .NET 6+)
namespace Lacesong.Core.Services;

// 3. class declaration
public class ServiceName
{
    // 4. constants
    private const string ConstantValue = "value";
    
    // 5. fields
    private readonly IDependency _dependency;
    private string _state;
    
    // 6. constructor
    public ServiceName(IDependency dependency)
    {
        _dependency = dependency;
    }
    
    // 7. properties
    public string PropertyName { get; set; }
    
    // 8. public methods
    public void PublicMethod() { }
    
    // 9. private methods
    private void PrivateMethod() { }
}
```

### MVVM Patterns

For Avalonia ViewModels:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MyViewModel : BaseViewModel
{
    // use source generators for observable properties
    [ObservableProperty]
    private string _myProperty;
    
    // explicitly define commands for clarity
    public IAsyncRelayCommand MyCommandAsync { get; }
    
    public MyViewModel(ILogger<MyViewModel> logger) : base(logger)
    {
        // initialize commands in constructor
        MyCommandAsync = new AsyncRelayCommand(ExecuteMyCommandAsync);
    }
    
    private async Task ExecuteMyCommandAsync()
    {
        await ExecuteAsync(async () =>
        {
            // command implementation
        });
    }
}
```

### Async/Await

- Always use `async`/`await` for I/O-bound operations
- Suffix async methods with `Async`
- Use `Task` or `Task<T>` return types
- Avoid `async void` except for event handlers

```csharp
// good
public async Task<OperationResult> DownloadFileAsync(string url)
{
    var response = await httpClient.GetAsync(url);
    return await ProcessResponseAsync(response);
}

// bad
public OperationResult DownloadFile(string url)
{
    var response = httpClient.GetAsync(url).Result; // blocking!
    return ProcessResponse(response);
}
```

### Error Handling

- Use `OperationResult` pattern for operations that can fail
- Log errors appropriately
- Provide meaningful error messages

```csharp
public async Task<OperationResult> PerformOperation()
{
    try
    {
        // operation logic
        return OperationResult.SuccessResult("Operation completed");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to perform operation");
        return OperationResult.ErrorResult(
            $"Operation failed: {ex.Message}", 
            "Error category"
        );
    }
}
```

### Platform-Specific Code

Use runtime detection for cross-platform code:

```csharp
using System.Runtime.InteropServices;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    // windows-specific logic
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    // macos-specific logic
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    // linux-specific logic
}
```

## Testing

### Test Requirements

- All new features must include tests
- Bug fixes should include regression tests
- Maintain or improve code coverage
- Tests should be clear, focused, and maintainable

### Test Structure

```csharp
using Xunit;
using Moq;

public class ServiceTests
{
    [Fact]
    public async Task MethodName_Scenario_ExpectedBehavior()
    {
        // arrange
        var mockDependency = new Mock<IDependency>();
        var service = new Service(mockDependency.Object);
        
        // act
        var result = await service.MethodName();
        
        // assert
        Assert.True(result.Success);
        Assert.Equal("expected", result.Data);
    }
}
```

### Running Tests

```bash
# run all tests
dotnet test

# run specific test project
dotnet test tests/Lacesong.Tests

# run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Commit Guidelines

### Commit Message Format

Follow conventional commits:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: new feature
- `fix`: bug fix
- `docs`: documentation changes
- `style`: code style changes (formatting, no logic change)
- `refactor`: code refactoring
- `test`: adding or updating tests
- `chore`: maintenance tasks, dependencies

**Examples:**

```
feat(bepinex): add version selection for installation

Implemented version dropdown in BepInEx install view.
Users can now choose from available BepInEx versions.

Closes #123
```

```
fix(game-detection): handle macos .app bundles correctly

Fixed path detection for macOS game installations packaged
as .app bundles.

Fixes #456
```

```
docs: update contributing guidelines

Added code style section and testing requirements.
```

### Commit Best Practices

- Keep commits atomic and focused
- Write clear, descriptive messages
- Reference issues when applicable
- Sign your commits if possible

## Pull Request Process

### Before Submitting

1. **Update your fork:**
   ```bash
   git checkout main
   git pull upstream main
   git push origin main
   ```

2. **Create a feature branch:**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Make your changes:**
   - Follow code style guidelines
   - Write tests
   - Update documentation

4. **Test your changes:**
   ```bash
   dotnet test
   ./build.sh  # or build.bat on windows
   ```

5. **Commit your changes:**
   ```bash
   git add .
   git commit -m "feat: your feature description"
   ```

6. **Push to your fork:**
   ```bash
   git push origin feature/your-feature-name
   ```

### Submitting the PR

1. Go to the [Lacesong repository](https://github.com/pecansolutions/lacesong)
2. Click "New Pull Request"
3. Select your fork and branch
4. Fill out the PR template:
   - **Title**: Clear, descriptive title
   - **Description**: What changes were made and why
   - **Issue Reference**: Link related issues
   - **Testing**: How you tested the changes
   - **Screenshots**: If applicable

### PR Review Process

- Maintainers will review your PR
- Address any feedback or requested changes
- Once approved, a maintainer will merge your PR
- Your contribution will be included in the next release

### PR Checklist

- [ ] Code follows project style guidelines
- [ ] Self-review of code completed
- [ ] Comments added for complex logic
- [ ] Documentation updated (if needed)
- [ ] Tests added/updated and passing
- [ ] No new warnings or errors
- [ ] Commit messages follow guidelines
- [ ] Branch is up-to-date with main

## Issue Reporting

### Bug Reports

Include:
- **Description**: Clear description of the bug
- **Steps to Reproduce**: Detailed steps
- **Expected Behavior**: What should happen
- **Actual Behavior**: What actually happens
- **Environment**:
  - OS and version
  - .NET version
  - Lacesong version
- **Logs**: Relevant log files or error messages
- **Screenshots**: If applicable

### Feature Requests

Include:
- **Description**: What feature you'd like
- **Use Case**: Why this feature is needed
- **Proposed Solution**: How you envision it working
- **Alternatives**: Other approaches considered

## Architecture Overview

### Core Principles

- **Separation of Concerns**: Clear boundaries between layers
- **Dependency Injection**: Services injected via constructor
- **MVVM Pattern**: ViewModels mediate between Views and Services
- **Platform Agnostic Core**: Business logic independent of UI
- **Async/Await**: Non-blocking operations throughout

### Layer Responsibilities

**Lacesong.Core**
- Business logic
- Service implementations
- Models and interfaces
- Platform-agnostic operations

**Lacesong.Avalonia / Lacesong.WPF**
- UI layer
- ViewModels (MVVM)
- Views (XAML)
- Platform-specific services

**Lacesong.CLI**
- Command-line interface
- CLI-specific commands
- Uses Core services

### Key Services

- `IGameDetector`: Detects game installations across platforms
- `IBepInExManager`: Manages BepInEx installation and configuration
- `IModManager`: Handles mod installation, updates, and conflicts
- `IGameStateService`: Maintains current game state
- `INavigationService`: Handles view navigation
- `IDialogService`: Shows dialogs and messages

## Questions or Need Help?

- Open an issue with the `question` label
- Email: wwdarrenwei@gmail.com
- Discord: stitchsages

Thank you for contributing to Lacesong!


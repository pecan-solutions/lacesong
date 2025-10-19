using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.WPF.Services;
using Lacesong.WPF.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;
using Application = System.Windows.Application;
using Window = System.Windows.Window;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lacesong.WPF.Tests;

/*
/// <summary>
/// ui automation tests for the wpf application
/// </summary>
public class UIAutomationTests : IDisposable
{
    private readonly FlaUI.Core.Application _application;
    private readonly UIA3Automation _automation;
    private readonly FlaUI.Core.AutomationElements.Window _mainWindow;

    public UIAutomationTests()
    {
        // setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // create and start application
        var app = Application.Current ?? new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // create main window
        var mainViewModel = serviceProvider.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow(mainViewModel);
        mainWindow.Show();

        // setup automation
        _application = FlaUI.Core.Application.Attach(Process.GetCurrentProcess().Id);
        _automation = new UIA3Automation();
        _mainWindow = _application.GetMainWindow(_automation);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // mock services
        var mockGameDetector = new Mock<IGameDetector>();
        var mockBepInExManager = new Mock<IBepInExManager>();
        var mockModManager = new Mock<IModManager>();
        var mockBackupManager = new Mock<IBackupManager>();
        var mockDialogService = new Mock<IDialogService>();
        var mockUpdateService = new Mock<IUpdateService>();

        // setup mock behaviors
        mockGameDetector.Setup(x => x.DetectGameInstall(It.IsAny<string>()))
                       .ReturnsAsync(new GameInstallation
                       {
                           Name = "Hollow Knight: Silksong",
                           Id = "test-game",
                           InstallPath = "C:\\Test\\Game",
                           Executable = "TestGame.exe",
                           IsValid = true
                       });

        mockBepInExManager.Setup(x => x.IsBepInExInstalled(It.IsAny<GameInstallation>()))
                         .Returns(false);

        mockModManager.Setup(x => x.GetInstalledMods(It.IsAny<GameInstallation>()))
                     .ReturnsAsync(new List<ModInfo>());

        mockUpdateService.Setup(x => x.CheckForUpdatesAsync())
                        .ReturnsAsync(new UpdateInfo { IsUpdateAvailable = false });

        // register services
        services.AddSingleton(mockGameDetector.Object);
        services.AddSingleton(mockBepInExManager.Object);
        services.AddSingleton(mockModManager.Object);
        services.AddSingleton(mockBackupManager.Object);
        services.AddSingleton(mockDialogService.Object);
        services.AddSingleton(mockUpdateService.Object);

        // register view models
        services.AddTransient<MainViewModel>();
        services.AddTransient<GameDetectionViewModel>();
        services.AddTransient<BepInExInstallViewModel>();
        services.AddTransient<ModCatalogViewModel>();
        services.AddTransient<SettingsViewModel>();

        // logging
        services.AddLogging(builder => builder.AddConsole());
    }

    [Fact]
    public void MainWindow_ShouldLoadSuccessfully()
    {
        // arrange
        var window = _automation.FromHandle(_mainWindow.Handle);

        // act & assert
        Assert.NotNull(window);
        Assert.True(window.IsEnabled);
        Assert.True(window.IsOffscreen == false);
    }

    [Fact]
    public void NavigationButtons_ShouldBePresent()
    {
        // arrange
        var window = _automation.FromHandle(_mainWindow.Handle);

        // act
        var gameDetectionButton = window.FindFirstDescendant(cf => cf.ByText("🔍 Game Detection"));
        var bepinexButton = window.FindFirstDescendant(cf => cf.ByText("⚡ BepInEx Install"));
        var modCatalogButton = window.FindFirstDescendant(cf => cf.ByText("📦 Mod Catalog"));

        // assert
        Assert.NotNull(gameDetectionButton);
        Assert.NotNull(bepinexButton);
        Assert.NotNull(modCatalogButton);
    }

    [Fact]
    public void SettingsButton_ShouldOpenSettings()
    {
        // arrange
        var window = _automation.FromHandle(_mainWindow.Handle);
        var settingsButton = window.FindFirstDescendant(cf => cf.ByText("⚙️ Settings"));

        // act
        settingsButton?.Click();

        // assert
        // verify that settings view is displayed
        var settingsView = window.FindFirstDescendant(cf => cf.ByText("⚙️ Settings"));
        Assert.NotNull(settingsView);
    }

    [Fact]
    public void GameDetectionView_ShouldDisplayCorrectly()
    {
        // arrange
        var window = _automation.FromHandle(_mainWindow.Handle);
        var gameDetectionButton = window.FindFirstDescendant(cf => cf.ByText("🔍 Game Detection"));

        // act
        gameDetectionButton?.Click();

        // assert
        var detectButton = window.FindFirstDescendant(cf => cf.ByText("🔍 Detect Games"));
        var browseButton = window.FindFirstDescendant(cf => cf.ByText("📁 Browse for Game"));
        
        Assert.NotNull(detectButton);
        Assert.NotNull(browseButton);
    }

    [Fact]
    public void BepInExInstallView_ShouldDisplayCorrectly()
    {
        // arrange
        var window = _automation.FromHandle(_mainWindow.Handle);
        var bepinexButton = window.FindFirstDescendant(cf => cf.ByText("⚡ BepInEx Install"));

        // act
        bepinexButton?.Click();

        // assert
        var installButton = window.FindFirstDescendant(cf => cf.ByText("📥 Install BepInEx"));
        var refreshButton = window.FindFirstDescendant(cf => cf.ByText("🔄 Refresh Status"));
        
        Assert.NotNull(installButton);
        Assert.NotNull(refreshButton);
    }

    [Fact]
    public void ModCatalogView_ShouldDisplayCorrectly()
    {
        // arrange
        var window = _automation.FromHandle(_mainWindow.Handle);
        var modCatalogButton = window.FindFirstDescendant(cf => cf.ByText("📦 Mod Catalog"));

        // act
        modCatalogButton?.Click();

        // assert
        var installFileButton = window.FindFirstDescendant(cf => cf.ByText("📁 Install from File"));
        var installUrlButton = window.FindFirstDescendant(cf => cf.ByText("🌐 Install from URL"));
        var refreshButton = window.FindFirstDescendant(cf => cf.ByText("🔄 Refresh"));
        
        Assert.NotNull(installFileButton);
        Assert.NotNull(installUrlButton);
        Assert.NotNull(refreshButton);
    }

    [Fact]
    public void StatusBar_ShouldDisplayStatus()
    {
        // arrange
        var window = _automation.FromHandle(_mainWindow.Handle);

        // act
        var statusBar = window.FindFirstDescendant(cf => cf.ByAutomationId("StatusBar"));

        // assert
        Assert.NotNull(statusBar);
    }

    [Fact]
    public void HeaderButtons_ShouldBePresent()
    {
        // arrange
        var window = _automation.FromHandle(_mainWindow.Handle);

        // act
        var openLogsButton = window.FindFirstDescendant(cf => cf.ByText("📁 Open Logs"));
        var settingsButton = window.FindFirstDescendant(cf => cf.ByText("⚙️ Settings"));
        var exitButton = window.FindFirstDescendant(cf => cf.ByText("❌ Exit"));

        // assert
        Assert.NotNull(openLogsButton);
        Assert.NotNull(settingsButton);
        Assert.NotNull(exitButton);
    }

    public void Dispose()
    {
        _mainWindow?.Close();
        _application?.Shutdown();
        _automation?.Dispose();
    }
}
*/
/// <summary>
/// view model tests for wpf functionality
/// </summary>
public class ViewModelTests
{
    [Fact]
    public void MainViewModel_ShouldInitializeCorrectly()
    {
        // arrange
        var mockLogger = new Mock<ILogger<MainViewModel>>();
        var mockGameDetector = new Mock<IGameDetector>();
        var mockDialogService = new Mock<IDialogService>();
        var mockUpdateService = new Mock<IUpdateService>();

        // act
        var viewModel = new MainViewModel(
            mockLogger.Object,
            mockGameDetector.Object,
            mockDialogService.Object,
            mockUpdateService.Object);

        // assert
        Assert.NotNull(viewModel);
        Assert.Equal("GameDetection", viewModel.CurrentView);
        Assert.False(viewModel.IsGameDetected);
    }

    [Fact]
    public async Task GameDetectionViewModel_ShouldDetectGames()
    {
        // arrange
        var mockLogger = new Mock<ILogger<GameDetectionViewModel>>();
        var mockGameDetector = new Mock<IGameDetector>();
        var mockDialogService = new Mock<IDialogService>();

        var testGame = new GameInstallation
        {
            Name = "Hollow Knight: Silksong",
            Id = "test-game",
            InstallPath = "C:\\Test\\Game",
            Executable = "TestGame.exe",
            IsValid = true
        };

        mockGameDetector.Setup(x => x.GetSupportedGames())
                       .ReturnsAsync(new List<GameInstallation> { testGame });

        mockGameDetector.Setup(x => x.DetectGameInstall(It.IsAny<string>()))
                       .ReturnsAsync(testGame);

        var viewModel = new GameDetectionViewModel(
            mockLogger.Object,
            mockGameDetector.Object,
            mockDialogService.Object);

        // act
        await viewModel.DetectGamesCommand.ExecuteAsync(null);

        // assert
        Assert.NotEmpty(viewModel.DetectedGames);
        Assert.Equal("Hollow Knight: Silksong", viewModel.DetectedGames.First().Name);
    }

    [Fact]
    public void BepInExInstallViewModel_ShouldCheckStatus()
    {
        // arrange
        var mockLogger = new Mock<ILogger<BepInExInstallViewModel>>();
        var mockBepInExManager = new Mock<IBepInExManager>();
        var mockDialogService = new Mock<IDialogService>();

        var gameInstall = new GameInstallation
        {
            Name = "Test Game",
            InstallPath = "C:\\Test\\Game",
            Executable = "TestGame.exe",
            IsValid = true
        };

        mockBepInExManager.Setup(x => x.IsBepInExInstalled(It.IsAny<GameInstallation>()))
                         .Returns(false);

        var viewModel = new BepInExInstallViewModel(
            mockLogger.Object,
            mockBepInExManager.Object,
            mockDialogService.Object);

        // act
        viewModel.SetGameInstallation(gameInstall);

        // assert
        Assert.False(viewModel.IsBepInExInstalled);
        Assert.Equal("BepInEx is not installed", viewModel.InstallationStatus);
    }

    [Fact]
    public void ModCatalogViewModel_ShouldLoadMods()
    {
        // arrange
        var mockLogger = new Mock<ILogger<ModCatalogViewModel>>();
        var mockModManager = new Mock<IModManager>();
        var mockModIndexService = new Mock<IModIndexService>();
        var mockModUpdateService = new Mock<IModUpdateService>();
        var mockConflictService = new Mock<IConflictDetectionService>();
        var mockCompatibilityService = new Mock<ICompatibilityService>();
        var mockConfigService = new Mock<IModConfigService>();
        var mockDialogService = new Mock<IDialogService>();

        var gameInstall = new GameInstallation
        {
            Name = "Test Game",
            InstallPath = "C:\\Test\\Game",
            Executable = "TestGame.exe",
            IsValid = true
        };

        var testMods = new List<ModInfo>
        {
            new ModInfo
            {
                Id = "test-mod",
                Name = "Test Mod",
                Version = "1.0.0",
                Description = "A test mod",
                Author = "Test Author",
                IsInstalled = true,
                IsEnabled = true
            }
        };

        mockModManager.Setup(x => x.GetInstalledMods(It.IsAny<GameInstallation>()))
                     .ReturnsAsync(testMods);

        var viewModel = new ModCatalogViewModel(
            mockLogger.Object,
            mockModManager.Object,
            mockModIndexService.Object,
            mockModUpdateService.Object,
            mockConflictService.Object,
            mockCompatibilityService.Object,
            mockConfigService.Object,
            mockDialogService.Object);

        // act
        viewModel.SetGameInstallation(gameInstall);

        // assert
        Assert.NotEmpty(viewModel.InstalledMods);
        Assert.Equal("Test Mod", viewModel.InstalledMods.First().Name);
    }

    [Fact]
    public void SettingsViewModel_ShouldLoadSettings()
    {
        // arrange
        var mockLogger = new Mock<ILogger<SettingsViewModel>>();
        var mockUpdateService = new Mock<IUpdateService>();

        var viewModel = new SettingsViewModel(
            mockLogger.Object,
            mockUpdateService.Object);

        // act & assert
        Assert.True(viewModel.AutoCheckForUpdates);
        Assert.True(viewModel.CreateBackupsBeforeInstall);
        Assert.False(viewModel.ShowAdvancedOptions);
        Assert.Equal("5.4.22", viewModel.BepinexVersion);
    }
}

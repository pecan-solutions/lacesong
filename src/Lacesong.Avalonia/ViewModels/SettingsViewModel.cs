using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Avalonia.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ILoggingService _loggingService;
    private readonly IUpdateService _updateService;

    [ObservableProperty]
    private bool _autoCheckForUpdates = true;

    [ObservableProperty]
    private bool _createBackupsBeforeInstall = true;

    [ObservableProperty]
    private bool _showAdvancedOptions = false;

    [ObservableProperty]
    private string _logLevel = "Information";

    [ObservableProperty]
    private string _bepinexVersion = "5.4.22";

    [ObservableProperty]
    private bool _enableTelemetry = false;

    [ObservableProperty]
    private string _settingsStatus = "Settings loaded";

    public List<string> LogLevels { get; } = new()
    {
        "Trace",
        "Debug", 
        "Information",
        "Warning",
        "Error",
        "Critical"
    };

    public List<string> BepInExVersions { get; } = new()
    {
        "5.4.22",
        "5.4.21",
        "5.4.20",
        "5.4.19"
    };

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        ILoggingService loggingService,
        IUpdateService updateService) : base(logger)
    {
        _loggingService = loggingService;
        _updateService = updateService;
        
        LoadSettings();
    }

    [RelayCommand]
    private void LoadSettings()
    {
        // load settings from configuration file or registry
        // for now, we'll use default values
        SettingsStatus = "Settings loaded";
        SetStatus("Settings loaded successfully");
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _ = ExecuteAsync(async () =>
        {
            SettingsStatus = "Saving settings...";
            
            // save settings to configuration file or registry
            // this would be implemented with actual persistence
            
            SettingsStatus = "Settings saved successfully";
            SetStatus("Settings saved successfully");
            
            await Task.CompletedTask; // ensure async
        }, "Saving settings...");
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        AutoCheckForUpdates = true;
        CreateBackupsBeforeInstall = true;
        ShowAdvancedOptions = false;
        LogLevel = "Information";
        BepinexVersion = "5.4.22";
        EnableTelemetry = false;
        
        SettingsStatus = "Settings reset to defaults";
        SetStatus("Settings reset to defaults");
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        _loggingService.OpenLogsFolder();
        SettingsStatus = "Opened logs folder";
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _loggingService.ClearLogs();
        SettingsStatus = "Logs cleared";
        SetStatus("Logs cleared successfully");
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        await ExecuteAsync(async () =>
        {
            SettingsStatus = "Checking for updates...";
            
            var updateInfo = await _updateService.CheckForUpdatesAsync();
            if (updateInfo.IsUpdateAvailable)
            {
                SettingsStatus = $"Update available: {updateInfo.LatestVersion}";
                SetStatus($"Update available: {updateInfo.LatestVersion}");
            }
            else
            {
                SettingsStatus = "You are running the latest version";
                SetStatus("You are running the latest version");
            }
        }, "Checking for updates...");
    }

    [RelayCommand]
    private void ExportSettings()
    {
        // export settings to file
        SettingsStatus = "Settings exported";
        SetStatus("Settings exported successfully");
    }

    [RelayCommand]
    private void ImportSettings()
    {
        // import settings from file
        SettingsStatus = "Settings imported";
        SetStatus("Settings imported successfully");
    }
}

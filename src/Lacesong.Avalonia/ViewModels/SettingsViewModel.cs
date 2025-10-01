using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Avalonia.Models;
using Lacesong.Avalonia.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly IUpdateService _updateService;

    [ObservableProperty]
    private Settings _currentSettings;

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
        ISettingsService settingsService,
        ILoggingService loggingService,
        IUpdateService updateService) : base(logger)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _updateService = updateService;

        _currentSettings = _settingsService.CurrentSettings;
        SetStatus("Settings loaded successfully");
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.SaveSettings();
        SetStatus("Settings saved successfully");
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        _settingsService.ResetToDefaults();
        CurrentSettings = _settingsService.CurrentSettings; // Refresh the bound object
        SetStatus("Settings reset to defaults");
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        _loggingService.OpenLogsFolder();
        SetStatus("Opened logs folder");
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _loggingService.ClearLogs();
        SetStatus("Logs cleared successfully");
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        await ExecuteAsync(async () =>
        {
            var updateInfo = await _updateService.CheckForUpdatesAsync();
            if (updateInfo.IsUpdateAvailable)
            {
                SetStatus($"Update available: {updateInfo.LatestVersion}");
            }
            else
            {
                SetStatus("You are running the latest version");
            }
        }, "Checking for updates...");
    }
}

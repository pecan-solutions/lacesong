using System;
using System.IO;
using System.Text.Json;
using Lacesong.Avalonia.Models;
using Microsoft.Extensions.Logging;

namespace Lacesong.Avalonia.Services;

public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsFilePath;
    
    public Settings CurrentSettings { get; private set; }

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var lacesongDataPath = Path.Combine(appDataPath, "Lacesong");
        Directory.CreateDirectory(lacesongDataPath);
        _settingsFilePath = Path.Combine(lacesongDataPath, "settings.json");
        
        CurrentSettings = new Settings();
        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                CurrentSettings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                _logger.LogInformation("Settings loaded from {FilePath}", _settingsFilePath);
            }
            else
            {
                _logger.LogInformation("Settings file not found. Using default settings.");
                CurrentSettings = new Settings();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings. Using default settings.");
            CurrentSettings = new Settings();
        }
    }

    public void SaveSettings()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(CurrentSettings, options);
            File.WriteAllText(_settingsFilePath, json);
            _logger.LogInformation("Settings saved to {FilePath}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings.");
        }
    }

    public void ResetToDefaults()
    {
        CurrentSettings = new Settings();
        SaveSettings();
        _logger.LogInformation("Settings have been reset to defaults.");
    }
}

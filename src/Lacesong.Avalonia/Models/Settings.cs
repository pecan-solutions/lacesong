namespace Lacesong.Avalonia.Models;

public class Settings
{
    public bool AutoCheckForUpdates { get; set; } = true;
    public bool CreateBackupsBeforeInstall { get; set; } = true;
    public bool ShowAdvancedOptions { get; set; } = false;
    public string LogLevel { get; set; } = "Information";
    public string BepinexVersion { get; set; } = "5.4.22";
    public bool EnableTelemetry { get; set; } = false;
}

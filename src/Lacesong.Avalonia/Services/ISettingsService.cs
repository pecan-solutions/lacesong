using Lacesong.Avalonia.Models;

namespace Lacesong.Avalonia.Services;

public interface ISettingsService
{
    Settings CurrentSettings { get; }
    void LoadSettings();
    void SaveSettings();
    void ResetToDefaults();
}

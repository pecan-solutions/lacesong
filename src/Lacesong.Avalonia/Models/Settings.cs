using CommunityToolkit.Mvvm.ComponentModel;

namespace Lacesong.Avalonia.Models;

public partial class Settings : ObservableObject
{
    [ObservableProperty]
    private bool _autoCheckForUpdates = true;

    [ObservableProperty]
    private bool _createBackupsBeforeInstall = true;

    [ObservableProperty]
    private bool _showAdvancedOptions = false;


    [ObservableProperty]
    private string _bepinexVersion = "5.4.22";


    [ObservableProperty]
    private string _theme = "The Marrow";

    [ObservableProperty]
    private string _preferredLaunchMode = "Modded";

    [ObservableProperty]
    private string _currentVersion = "1.0.0";
}

using System;
using System.Threading.Tasks;
using Lacesong.Core.Models;

namespace Lacesong.Avalonia.Services;

/// <summary>
/// interface for dialog services
/// </summary>
public interface IDialogService
{
    Task<string?> ShowFolderDialogAsync(string title);
    Task<string?> ShowOpenFileDialogAsync(string title, string filter);
    Task<string?> ShowSaveFileDialogAsync(string title, string filter);
    Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "");
    Task<bool> ShowConfirmationDialogAsync(string title, string message);
    Task ShowMessageDialogAsync(string title, string message);

    // opens per-mod settings window
    Task ShowModSettingsAsync(GameInstallation installation, string modId);
}

/// <summary>
/// interface for logging services
/// </summary>
public interface ILoggingService
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(Exception exception);
    void OpenLogsFolder();
    void ClearLogs();
}

/// <summary>
/// interface for update services
/// </summary>
public interface IUpdateService
{
    Task<UpdateInfo> CheckForUpdatesAsync();
    Task DownloadUpdateAsync();
    bool IsUpdateAvailable { get; }
    string LatestVersion { get; }
}

/// <summary>
/// update information
/// </summary>
public class UpdateInfo
{
    public bool IsUpdateAvailable { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
}

/// <summary>
/// service for navigating between views
/// </summary>
public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : class;
}

/// <summary>
/// service for showing snackbar notifications
/// </summary>
public interface ISnackbarService
{
    void Show(string title, string message, string type = "Information", string icon = "ℹ️", TimeSpan? duration = null);
}

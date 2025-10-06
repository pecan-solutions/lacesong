using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;

namespace Lacesong.Avalonia.Services;

public class SnackbarService : ISnackbarService
{
    private Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    private INotificationManager? _manager;

    private INotificationManager Manager
    {
        get
        {
            if (_manager == null && MainWindow != null)
            {
                _manager = new WindowNotificationManager(MainWindow)
                {
                    Position = NotificationPosition.TopRight,
                    MaxItems = 4,
                    Margin = new Thickness(0, 40, 20, 0)
                };
            }
            return _manager ?? throw new InvalidOperationException("Main window not ready");
        }
    }

    public void Show(string title, string message, string type = "Information", string icon = "ℹ️", TimeSpan? duration = null)
    {
        var notifType = type.ToLowerInvariant() switch
        {
            "success" => NotificationType.Success,
            "error" => NotificationType.Error,
            "warning" => NotificationType.Warning,
            _ => NotificationType.Information
        };
        duration ??= TimeSpan.FromSeconds(4);
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime) return;
        Dispatcher.UIThread.Post(() =>
        {
            Manager.Show(new Notification(title, message, notifType, duration, null));
        });
    }
}

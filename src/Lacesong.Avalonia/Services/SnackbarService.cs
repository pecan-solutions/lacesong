using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Lacesong.Avalonia.Services;

public class SnackbarService : ISnackbarService
{
    private Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public SnackbarService()
    {
    }

    public void Show(string title, string message, string type = "Information", string icon = "ℹ️", TimeSpan? duration = null)
    {
        // TODO: Implement snackbar notifications using Avalonia controls
        // for now, just write to debug output
        System.Diagnostics.Debug.WriteLine($"{icon} {title}: {message}");
    }
}

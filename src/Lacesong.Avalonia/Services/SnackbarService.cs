using System;

namespace Lacesong.Avalonia.Services;

public class SnackbarService : ISnackbarService
{
    public void Show(string title, string message, string type = "Information", string icon = "ℹ️", TimeSpan? duration = null)
    {
        // Placeholder implementation.
        // In a real application, this would show a toast notification or a snackbar.
        Console.WriteLine($"[Snackbar] {icon} {title}: {message}");
    }
}

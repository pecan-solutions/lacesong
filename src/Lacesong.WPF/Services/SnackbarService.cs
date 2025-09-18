using System;
using System.Threading.Tasks;

namespace Lacesong.WPF.Services;

/// <summary>
/// interface for snackbar notification service
/// </summary>
public interface ISnackbarService
{
    void Show(string title, string message, object appearance, object icon, TimeSpan duration);
}

/// <summary>
/// simple implementation of snackbar service using status messages
/// </summary>
public class SnackbarService : ISnackbarService
{
    public void Show(string title, string message, object appearance, object icon, TimeSpan duration)
    {
        // for now, just log the message since we don't have a proper snackbar implementation
        // this could be enhanced later with a proper toast notification system
        System.Diagnostics.Debug.WriteLine($"Snackbar: {title} - {message}");
    }
}

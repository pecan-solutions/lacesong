using System;
using System.Threading.Tasks;

namespace Lacesong.WPF.Services;

/// <summary>
/// interface for content dialog service
/// </summary>
public interface IContentDialogService
{
    Task<bool> ShowConfirmationDialogAsync(string title, string message);
    Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "");
    Task ShowMessageDialogAsync(string title, string message);
}

/// <summary>
/// simple implementation of content dialog service
/// </summary>
public class ContentDialogService : IContentDialogService
{
    public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        // simple implementation using MessageBox
        var result = System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.YesNo);
        return result == System.Windows.MessageBoxResult.Yes;
    }

    public async Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "")
    {
        // simple implementation - could be enhanced with proper input dialog
        System.Diagnostics.Debug.WriteLine($"Input Dialog: {title} - {message}");
        return defaultValue;
    }

    public async Task ShowMessageDialogAsync(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title);
        await Task.CompletedTask;
    }
}

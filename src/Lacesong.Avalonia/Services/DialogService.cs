using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Lacesong.Avalonia.Views;
using Lacesong.Core.Models;

namespace Lacesong.Avalonia.Services;

public class DialogService : IDialogService
{
    private Window MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow ?? throw new InvalidOperationException("Cannot find MainWindow");

    public async Task<string?> ShowFolderDialogAsync(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return await dialog.ShowAsync(MainWindow);
    }

    public async Task<string?> ShowOpenFileDialogAsync(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            AllowMultiple = false
        };

        if (!string.IsNullOrEmpty(filter))
        {
            var filterPattern = filter.Split('|');
            var fileDialogFilters = new List<FileDialogFilter>();
            for (int i = 0; i < filterPattern.Length; i += 2)
            {
                fileDialogFilters.Add(new FileDialogFilter { Name = filterPattern[i], Extensions = new List<string> { filterPattern[i + 1].Replace("*.", "") } });
            }
            dialog.Filters = fileDialogFilters;
        }

        var result = await dialog.ShowAsync(MainWindow);
        return result?.Length > 0 ? result[0] : null;
    }

    public async Task<string?> ShowSaveFileDialogAsync(string title, string filter)
    {
        var dialog = new SaveFileDialog { Title = title };

        if (!string.IsNullOrEmpty(filter))
        {
            var filterPattern = filter.Split('|');
            var fileDialogFilters = new List<FileDialogFilter>();
            for (int i = 0; i < filterPattern.Length; i += 2)
            {
                fileDialogFilters.Add(new FileDialogFilter { Name = filterPattern[i], Extensions = new List<string> { filterPattern[i + 1].Replace("*.", "") } });
            }
            dialog.Filters = fileDialogFilters;
        }

        return await dialog.ShowAsync(MainWindow);
    }

    public async Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "")
    {
        // TODO: Implement input dialog using Avalonia controls
        return await Task.FromResult<string?>(null);
    }

    public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        // TODO: Implement confirmation dialog using Avalonia controls
        return await Task.FromResult(false);
    }

    public async Task ShowMessageDialogAsync(string title, string message)
    {
        // TODO: Implement message dialog using Avalonia controls
        await Task.CompletedTask;
    }
}

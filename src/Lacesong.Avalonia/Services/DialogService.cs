using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Lacesong.Avalonia.Views;
using Lacesong.Core.Models;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;

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
        var messageBoxStandardWindow = MessageBoxManager
            .GetMessageBoxInputWindow(new MessageBoxInputParams
            {
                ButtonDefinitions = new[]
                {
                    new ButtonDefinition { Name = "OK", IsDefault = true },
                    new ButtonDefinition { Name = "Cancel", IsCancel = true }
                },
                ContentTitle = title,
                ContentMessage = message,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            });

        var result = await messageBoxStandardWindow.ShowDialog(MainWindow);

        if (result.Button == "OK")
        {
            return result.Message;
        }

        return null;
    }

    public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        var messageBoxStandardWindow = MessageBoxManager
            .GetMessageBoxStandardWindow(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNo,
                ContentTitle = title,
                ContentMessage = message,
                Icon = Icon.Question,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            });

        var result = await messageBoxStandardWindow.ShowDialog(MainWindow);
        return result == ButtonResult.Yes;
    }

    public async Task ShowMessageDialogAsync(string title, string message)
    {
        var messageBoxStandardWindow = MessageBoxManager
            .GetMessageBoxStandardWindow(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.Ok,
                ContentTitle = title,
                ContentMessage = message,
                Icon = Icon.Info,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            });

        await messageBoxStandardWindow.ShowDialog(MainWindow);
    }

    public async Task ShowModSettingsAsync(GameInstallation installation, string modId)
    {
        var settingsWindow = new ModSettingsWindow
        {
            // DataContext will be set to the appropriate ViewModel
        };
        await settingsWindow.ShowDialog(MainWindow);
    }
}

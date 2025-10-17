using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
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
        var result = false;
        var tcs = new TaskCompletionSource<bool>();

        // create overlay panel
        var overlay = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)), // semi-transparent black
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // create modal content
        var modalContent = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), // dark background
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(24),
            MinWidth = 400,
            MaxWidth = 500,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // title
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // message
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // buttons

        // title
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            Margin = new Thickness(0, 0, 0, 16),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(titleText, 0);

        // message
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            Margin = new Thickness(0, 0, 0, 24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(messageText, 1);

        // centered button panel
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12
        };
        Grid.SetRow(buttonPanel, 2);

        var yesButton = new Button
        {
            Content = "Yes",
            Width = 100,
            Height = 36,
            FontSize = 14,
            FontWeight = FontWeight.Medium
        };
        yesButton.Classes.Add("primary");

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 100,
            Height = 36,
            FontSize = 14,
            FontWeight = FontWeight.Medium
        };

        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(cancelButton);

        contentGrid.Children.Add(titleText);
        contentGrid.Children.Add(messageText);
        contentGrid.Children.Add(buttonPanel);

        modalContent.Child = contentGrid;
        overlay.Children.Add(modalContent);

        // add overlay to main window
        Panel? mainPanel = null;
        if (MainWindow.Content is Panel panel)
        {
            mainPanel = panel;
            mainPanel.Children.Add(overlay);
        }

        // handle button clicks
        yesButton.Click += (s, e) =>
        {
            result = true;
            if (mainPanel != null)
            {
                mainPanel.Children.Remove(overlay);
            }
            tcs.SetResult(true);
        };

        cancelButton.Click += (s, e) =>
        {
            result = false;
            if (mainPanel != null)
            {
                mainPanel.Children.Remove(overlay);
            }
            tcs.SetResult(false);
        };

        // handle clicking outside the modal
        overlay.PointerPressed += (s, e) =>
        {
            if (e.Source == overlay)
            {
                result = false;
                if (mainPanel != null)
                {
                    mainPanel.Children.Remove(overlay);
                }
                tcs.SetResult(false);
            }
        };

        await tcs.Task;
        return result;
    }

    public async Task ShowMessageDialogAsync(string title, string message)
    {
        // TODO: Implement message dialog using Avalonia controls
        await Task.CompletedTask;
    }
}

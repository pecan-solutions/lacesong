using Microsoft.Win32;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace Lacesong.WPF.Services;

/// <summary>
/// implementation of dialog service for wpf
/// </summary>
public class DialogService : IDialogService
{
    public async Task<string?> ShowFolderDialogAsync(string title)
    {
        return await Task.Run(() =>
        {
            // simple folder picker using OpenFileDialog with a workaround
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "Folders|*",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                var directory = System.IO.Path.GetDirectoryName(dialog.FileName);
                return directory;
            }
            return null;
        });
    }

    public async Task<string?> ShowOpenFileDialogAsync(string title, string filter)
    {
        return await Task.Run(() =>
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                Multiselect = false
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        });
    }

    public async Task<string?> ShowSaveFileDialogAsync(string title, string filter)
    {
        return await Task.Run(() =>
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        });
    }

    public async Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "")
    {
        return await Task.Run(() =>
        {
            // simple input dialog implementation
            var inputDialog = new InputDialog(title, message, defaultValue);
            return inputDialog.ShowDialog() == true ? inputDialog.InputText : null;
        });
    }

    public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        return await Task.Run(() =>
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        });
    }

    public async Task ShowMessageDialogAsync(string title, string message)
    {
        await Task.Run(() =>
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    public async Task ShowModSettingsAsync(Lacesong.Core.Models.GameInstallation installation, string modId)
    {
        await Task.Run(() =>
        {
            var window = new Lacesong.WPF.Views.ModSettingsWindow(installation, modId);
            window.Owner = Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        });
    }

    public async Task ShowModDetailsAsync(Lacesong.Core.Models.ThunderstorePackage package)
    {
        await Task.Run(() =>
        {
            // build basic detail string using available properties
            var latestVersion = package.Versions.FirstOrDefault();
            var versionNumber = latestVersion?.VersionNumber ?? "unknown";
            var description = latestVersion?.Description ?? "no description";
            var details = $"{package.FullName}\nversion: {versionNumber}\nauthor: {package.Owner}\n\n{description}";
            MessageBox.Show(details, "Mod Details", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }
}

/// <summary>
/// simple input dialog for wpf
/// </summary>
public partial class InputDialog : Window
{
    public string InputText { get; private set; } = string.Empty;

    public InputDialog(string title, string message, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        MessageTextBlock.Text = message;
        InputTextBox.Text = defaultValue;
        InputTextBox.SelectAll();
    }

    private void InitializeComponent()
    {
        Width = 400;
        Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var grid = new System.Windows.Controls.Grid();
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

        MessageTextBlock = new System.Windows.Controls.TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(MessageTextBlock, 0);

        InputTextBox = new System.Windows.Controls.TextBox
        {
            Margin = new Thickness(10, 0, 10, 10),
            Height = 25
        };
        Grid.SetRow(InputTextBox, 1);

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10)
        };

        var okButton = new System.Windows.Controls.Button
        {
            Content = "OK",
            Width = 75,
            Height = 25,
            Margin = new Thickness(5, 0, 0, 0),
            IsDefault = true
        };
        okButton.Click += (s, e) => { InputText = InputTextBox.Text; DialogResult = true; };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Cancel",
            Width = 75,
            Height = 25,
            Margin = new Thickness(5, 0, 0, 0),
            IsCancel = true
        };
        cancelButton.Click += (s, e) => DialogResult = false;

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 2);

        grid.Children.Add(MessageTextBlock);
        grid.Children.Add(InputTextBox);
        grid.Children.Add(buttonPanel);

        Content = grid;
    }

    private System.Windows.Controls.TextBlock MessageTextBlock { get; set; } = null!;
    private System.Windows.Controls.TextBox InputTextBox { get; set; } = null!;
}
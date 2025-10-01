using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.Services
{
    public class ContentDialogService : IContentDialogService
    {
        private Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        public ContentDialogService()
        {
        }

        public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            // TODO: Implement confirmation dialog using Avalonia controls
            return await Task.FromResult(false);
        }

        public async Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "")
        {
            // TODO: Implement input dialog using Avalonia controls
            return await Task.FromResult<string?>(null);
        }

        public async Task ShowMessageDialogAsync(string title, string message)
        {
            // TODO: Implement message dialog using Avalonia controls
            await Task.CompletedTask;
        }

        public async Task ShowDialogAsync(object content)
        {
            // fallback generic content dialog using avalonia window wrapper
            var dialogWindow = new Window
            {
                Content = content,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (MainWindow is not null)
                await dialogWindow.ShowDialog(MainWindow);
            else
                dialogWindow.Show();
        }
    }
}

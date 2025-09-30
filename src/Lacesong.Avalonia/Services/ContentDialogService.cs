using Avalonia.Controls;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.Services
{
    public class ContentDialogService : IContentDialogService
    {
        private readonly MainWindow _mainWindow;

        public ContentDialogService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public async Task ShowDialogAsync(object content)
        {
            var dialog = _mainWindow.FindControl<ContentDialog>("DialogHost");
            if (dialog != null)
            {
                dialog.Content = content;
                await dialog.ShowAsync();
            }
        }
    }
}

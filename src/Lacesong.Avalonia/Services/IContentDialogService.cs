using System.Threading.Tasks;

namespace Lacesong.Avalonia.Services
{
    public interface IContentDialogService
    {
        Task<bool> ShowConfirmationDialogAsync(string title, string message);

        Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "");

        Task ShowMessageDialogAsync(string title, string message);

        // optional generic content dialog
        Task ShowDialogAsync(object content);
    }
}

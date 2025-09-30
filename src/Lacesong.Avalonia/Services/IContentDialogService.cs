using System.Threading.Tasks;

namespace Lacesong.Avalonia.Services
{
    public interface IContentDialogService
    {
        Task ShowDialogAsync(object content);
    }
}

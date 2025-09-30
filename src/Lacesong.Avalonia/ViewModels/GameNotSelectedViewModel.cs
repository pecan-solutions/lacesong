using CommunityToolkit.Mvvm.ComponentModel;
using Lacesong.Avalonia.Services;
using Microsoft.Extensions.Logging;

namespace Lacesong.Avalonia.ViewModels;

public partial class GameNotSelectedViewModel : BaseViewModel
{
    public GameNotSelectedViewModel(ILogger<GameNotSelectedViewModel> logger, ISnackbarService snackbarService)
        : base(logger)
    {
        StatusMessage = "no game selected";
        snackbarService.Show(
            "Game Required",
            "Please select a game installation before proceeding.",
            "Warning",
            "⚠️",
            System.TimeSpan.FromSeconds(5));
    }

    [ObservableProperty]
    private string _statusMessage;
}

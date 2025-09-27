using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Models;
using Lacesong.WPF.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lacesong.WPF.ViewModels;

// main view model backing the main window
public partial class MainViewModel : BaseViewModel
{
    private readonly IServiceProvider _services;

    public MainViewModel(
        ILogger<MainViewModel> logger,
        IServiceProvider services)
        : base(logger)
    {
        _services = services;
        // default view model
        CurrentViewModel = _services.GetRequiredService<HomeViewModel>();
    }

    [ObservableProperty]
    private object? _currentViewModel;

    // represents the currently selected/active game
    [ObservableProperty]
    private GameInstallation _currentGame = new();

    // command used by sidebar buttons to switch views
    public IRelayCommand NavigateCommand => new RelayCommand<Type>(Navigate);

    private void Navigate(Type? viewModelType)
    {
        if (viewModelType is null)
            return;

        try
        {
            var vm = _services.GetRequiredService(viewModelType);
            CurrentViewModel = vm;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "failed to navigate to {ViewModel}", viewModelType.Name);
        }
    }
}

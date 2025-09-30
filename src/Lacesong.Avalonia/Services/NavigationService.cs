using Lacesong.Avalonia.ViewModels;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Lacesong.Avalonia.Services;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        mainViewModel.CurrentView = _serviceProvider.GetRequiredService(typeof(TViewModel));
    }
}

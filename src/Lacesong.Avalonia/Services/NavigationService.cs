using CommunityToolkit.Mvvm.ComponentModel;
using Lacesong.Avalonia.ViewModels;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Lacesong.Avalonia.Services;

public partial class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private object? _currentViewModel;
    
    public event Action? CurrentViewModelChanged;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        var vm = _serviceProvider.GetRequiredService(typeof(TViewModel));
        SetCurrentViewModel(vm);
    }
    
    public void NavigateTo(Type viewModelType)
    {
        var vm = _serviceProvider.GetRequiredService(viewModelType);
        SetCurrentViewModel(vm);
    }

    private void SetCurrentViewModel(object? viewModel)
    {
        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke();
    }
}

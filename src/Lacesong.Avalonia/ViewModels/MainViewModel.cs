using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Avalonia.Services;
using Lacesong.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Lacesong.Avalonia.ViewModels;

public class NavigationItem : ObservableObject
{
    private bool _isActive;

    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public Type ViewModelType { get; set; }
    public bool IsActive 
    { 
        get => _isActive; 
        set 
        { 
            SetProperty(ref _isActive, value);
            OnPropertyChanged(nameof(ActiveClass));
        }
    }

    public string ActiveClass => IsActive ? "active" : "";

    // command that triggers navigation to this item's view model
    public IRelayCommand? NavigateCommand { get; set; }
}

// main view model backing the main window
public partial class MainViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly IGameStateService _gameStateService;
    private INotifyPropertyChanged? _trackedViewModel;

    public ObservableCollection<NavigationItem> NavigationItems { get; }
    
    public object? CurrentViewModel => _navigationService.CurrentViewModel;
    public GameInstallation CurrentGame => _gameStateService.CurrentGame;
    public bool IsGameDetected => _gameStateService.IsGameDetected;
    public string GameDetectionStatus => IsGameDetected ? $"Game: {CurrentGame.Name} - {CurrentGame.InstallPath}" : "Game not detected";

    public MainViewModel(ILogger<MainViewModel> logger, INavigationService navigationService, IGameStateService gameStateService) : base(logger)
    {
        _navigationService = navigationService;
        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
        
        _gameStateService = gameStateService;
        _gameStateService.GameStateChanged += OnGameStateChanged;

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            CreateNavItem("Home", "M10 2.5l-8 6v8h6v-6h4v6h6v-8l-8-6z", typeof(HomeViewModel)),
            CreateNavItem("Game Detection", "M4 8h2v2H4zm4 0h2v2H8zm4 0h2v2h-2zm-8 4h2v2H4zm4 0h2v2H8zm4 0h2v2h-2zM6 4h2v2H6zm4 0h2v2h-2zm4 0h2v2h-2z", typeof(GameDetectionViewModel)),
            CreateNavItem("Manage Mods", "M4 2h16v2H4zm0 4h10v2H4zm0 4h16v2H4zm0 4h10v2H4zm0 4h16v2H4z", typeof(ManageModsViewModel)),
            CreateNavItem("BepInEx", "M4 4h16v2H4zm0 4h16v2H4zm0 4h16v2H4zm0 4h16v2H4z", typeof(BepInExInstallViewModel)),
            CreateNavItem("Browse Mods", "M6 2h12v2H6zm0 4h12v2H6zm0 4h12v2H6zm0 4h8v2H6z", typeof(BrowseModsViewModel)),
            CreateNavItem("Settings", "M9.5 4c-1.4 0-2.5 1.1-2.5 2.5S8.1 9 9.5 9s2.5-1.1 2.5-2.5S10.9 4 9.5 4zm0 1c.8 0 1.5.7 1.5 1.5S10.3 8 9.5 8s-1.5-.7-1.5-1.5S8.7 5 9.5 5zM4.5 9C3.1 9 2 10.1 2 11.5S3.1 14 4.5 14s2.5-1.1 2.5-2.5S5.9 9 4.5 9zm0 1c.8 0 1.5.7 1.5 1.5s-.7 1.5-1.5 1.5-1.5-.7-1.5-1.5.7-1.5 1.5-1.5zm10 0c-1.4 0-2.5 1.1-2.5 2.5s1.1 2.5 2.5 2.5 2.5-1.1 2.5-2.5-1.1-2.5-2.5-2.5zm0 1c.8 0 1.5.7 1.5 1.5s-.7 1.5-1.5 1.5-1.5-.7-1.5-1.5.7-1.5 1.5-1.5z", typeof(SettingsViewModel))
        };

        // Navigate to the home view model on startup
        _navigationService.NavigateTo<HomeViewModel>();
        
        // ensure initial active state is set
        UpdateActiveNavigationItem();
    }
    
    private void OnGameStateChanged()
    {
        OnPropertyChanged(nameof(CurrentGame));
        OnPropertyChanged(nameof(IsGameDetected));
        OnPropertyChanged(nameof(GameDetectionStatus));
    }

    private void OnCurrentViewModelChanged()
    {
        if (_trackedViewModel != null)
        {
            _trackedViewModel.PropertyChanged -= CurrentViewModel_PropertyChanged;
        }

        OnPropertyChanged(nameof(CurrentViewModel));

        // update active navigation item
        UpdateActiveNavigationItem();

        if (CurrentViewModel is INotifyPropertyChanged newVm)
        {
            _trackedViewModel = newVm;
            _trackedViewModel.PropertyChanged += CurrentViewModel_PropertyChanged;
        }

        if (CurrentViewModel is BaseViewModel bvm)
        {
            StatusMessage = bvm.StatusMessage;
        }
        else
        {
            StatusMessage = string.Empty;
        }
    }
    
    private void CurrentViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StatusMessage) && sender is BaseViewModel vm)
        {
            StatusMessage = vm.StatusMessage;
        }
    }

    [RelayCommand]
    private void NavigateTo(Type viewModelType)
    {
        _navigationService.NavigateTo(viewModelType);
    }
    
    [RelayCommand]
    private void GoToHome() => _navigationService.NavigateTo<HomeViewModel>();

    [RelayCommand]
    private void GoToGameDetection() => _navigationService.NavigateTo<GameDetectionViewModel>();

    [RelayCommand]
    private void GoToManageMods() => _navigationService.NavigateTo<ManageModsViewModel>();

    [RelayCommand]
    private void GoToBepInExInstall() => _navigationService.NavigateTo<BepInExInstallViewModel>();

    [RelayCommand]
    private void GoToBrowseMods() => _navigationService.NavigateTo<BrowseModsViewModel>();
    
    [RelayCommand]
    private void GoToSettings() => _navigationService.NavigateTo<SettingsViewModel>();

    private void UpdateActiveNavigationItem()
    {
        var currentType = CurrentViewModel?.GetType();
        foreach (var item in NavigationItems)
        {
            item.IsActive = item.ViewModelType == currentType;
        }
    }

    private NavigationItem CreateNavItem(string label, string icon, Type vmType)
    {
        var item = new NavigationItem
        {
            Label = label,
            Icon = icon,
            ViewModelType = vmType
        };
        item.NavigateCommand = new RelayCommand(() => _navigationService.NavigateTo(vmType));
        return item;
    }
}

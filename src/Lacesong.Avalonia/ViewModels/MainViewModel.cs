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
    public Type ViewModelType { get; set; } = null!;
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

    public BottomBarViewModel BottomBar { get; }

    public MainViewModel(ILogger<MainViewModel> logger, INavigationService navigationService, IGameStateService gameStateService, BottomBarViewModel bottomBarViewModel) : base(logger)
    {
        _navigationService = navigationService;
        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
        
        _gameStateService = gameStateService;
        _gameStateService.GameStateChanged += OnGameStateChanged;

        BottomBar = bottomBarViewModel;

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            CreateNavItem("Home", "M10 2.5l-8 6v8h6v-6h4v6h6v-8l-8-6z", typeof(HomeViewModel)),
            CreateNavItem("Game Detection", "M4,8H8V4A2,2 0 0,1 10,2H14A2,2 0 0,1 16,4V8H20A2,2 0 0,1 22,10V14A2,2 0 0,1 20,16H16V20A2,2 0 0,1 14,22H10A2,2 0 0,1 8,20V16H4A2,2 0 0,1 2,14V10A2,2 0 0,1 4,8Z", typeof(GameDetectionViewModel)),
            CreateNavItem("Manage Mods", "M20 10.3V8H4V21H11V19.13L19.39 10.74C19.57 10.56 19.78 10.42 20 10.3M15 13H9V11.5C9 11.22 9.22 11 9.5 11H14.5C14.78 11 15 11.22 15 11.5V13M21 7H3V3H21V7M22.85 14.19L21.87 15.17L19.83 13.13L20.81 12.15C21 11.95 21.33 11.95 21.53 12.15L22.85 13.47C23.05 13.67 23.05 14 22.85 14.19M19.13 13.83L21.17 15.87L15.04 22H13V19.96L19.13 13.83Z", typeof(ManageModsViewModel)),
            CreateNavItem("Browse Mods", "M14.19,14.19L6,18L9.81,9.81L18,6M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,10.9A1.1,1.1 0 0,0 10.9,12A1.1,1.1 0 0,0 12,13.1A1.1,1.1 0 0,0 13.1,12A1.1,1.1 0 0,0 12,10.9Z", typeof(BrowseModsViewModel)),
            CreateNavItem("BepInEx", "M4 4h16v2H4zm0 4h16v2H4zm0 4h16v2H4zm0 4h16v2H4z", typeof(BepInExInstallViewModel)),
            CreateNavItem("Settings", "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z", typeof(SettingsViewModel))
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

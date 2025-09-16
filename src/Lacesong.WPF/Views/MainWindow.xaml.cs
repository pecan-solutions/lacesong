using Lacesong.WPF.ViewModels;
using Lacesong.WPF.Views;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Lacesong.WPF;

/// <summary>
/// main window for the lacesong application
/// </summary>
public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        DataContext = viewModel;
        _serviceProvider = serviceProvider;
        _viewModel = viewModel;
        
        // subscribe to view changes
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentView))
            {
                UpdateCurrentView();
            }
        };
        
        // set initial view
        UpdateCurrentView();
    }

    private void UpdateCurrentView()
    {
        var viewName = _viewModel.CurrentView;
        UserControl view = viewName switch
        {
            "Home" => _serviceProvider.GetRequiredService<HomeView>(),
            "GameDetection" => _serviceProvider.GetRequiredService<GameDetectionView>(),
            "BepInExInstall" => _serviceProvider.GetRequiredService<BepInExInstallView>(),
            "ModCatalog" => CreateModCatalogView(),
            "Settings" => _serviceProvider.GetRequiredService<SettingsView>(),
            _ => _serviceProvider.GetRequiredService<HomeView>()
        };
        
        ContentArea.Content = view;
    }

    private ModCatalogView CreateModCatalogView()
    {
        var view = _serviceProvider.GetRequiredService<ModCatalogView>();
        var viewModel = _serviceProvider.GetRequiredService<ModCatalogViewModel>();
        
        // pass the game installation to the view model
        if (_viewModel.CurrentGame != null)
        {
            viewModel.SetGameInstallation(_viewModel.CurrentGame);
        }
        
        view.DataContext = viewModel;
        return view;
    }
}

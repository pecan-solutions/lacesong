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
            "ModCatalog" => _serviceProvider.GetRequiredService<ModCatalogView>(),
            "Settings" => _serviceProvider.GetRequiredService<SettingsView>(),
            _ => _serviceProvider.GetRequiredService<HomeView>()
        };
        
        ContentArea.Content = view;
    }
}

using Lacesong.WPF.ViewModels;
using Lacesong.WPF.Views;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace Lacesong.WPF;

/// <summary>
/// main window for the lacesong application
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Initialize the navigation
        RootNavigation.ServiceProvider = serviceProvider;
        RootNavigation.Loaded += (_, _) => RootNavigation.Navigate(typeof(HomeView));
    }
}

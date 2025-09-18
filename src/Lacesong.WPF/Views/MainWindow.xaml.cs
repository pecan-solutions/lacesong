using Lacesong.WPF.ViewModels;
using Lacesong.WPF.Views;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Lacesong.WPF;

/// <summary>
/// main window for the lacesong application
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

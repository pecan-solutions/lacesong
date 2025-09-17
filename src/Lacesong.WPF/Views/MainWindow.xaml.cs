using Lacesong.WPF.ViewModels;
using Lacesong.WPF.Views;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;
using System;

namespace Lacesong.WPF;

/// <summary>
/// main window for the lacesong application
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

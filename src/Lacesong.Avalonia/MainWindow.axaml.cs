using Avalonia.Controls;
using Lacesong.Avalonia.ViewModels;

namespace Lacesong.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
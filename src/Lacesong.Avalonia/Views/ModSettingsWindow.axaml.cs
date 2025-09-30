using Avalonia.Controls;
using Lacesong.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Interactivity;

namespace Lacesong.Avalonia.Views;

public partial class ModSettingsWindow : Window
{
    public ModSettingsWindow()
    {
        InitializeComponent();
    }

    public void CloseWindow(object sender, RoutedEventArgs args)
    {
        Close();
    }
}

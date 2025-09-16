using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.WPF.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace Lacesong.WPF.Views;

public partial class ModSettingsWindow : Window
{
    public ModSettingsWindow(GameInstallation install, string modId)
    {
        InitializeComponent();
        var vm = App.Current.Services.GetRequiredService<ModSettingsViewModelFactory>()(install, modId);
        DataContext = vm;
    }
}

public delegate ModSettingsViewModel ModSettingsViewModelFactory(GameInstallation install, string modId);

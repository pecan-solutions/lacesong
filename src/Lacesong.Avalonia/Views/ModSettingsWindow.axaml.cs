using Avalonia.Controls;
using Lacesong.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Lacesong.Avalonia.ViewModels;

namespace Lacesong.Avalonia.Views
{
    public partial class ModSettingsWindow : Window
    {
        public ModSettingsWindow(GameInstallation install, string modId)
        {
            InitializeComponent();
            var vm = ((App)App.Current).Services.GetRequiredService<ModSettingsViewModelFactory>()(install, modId);
            DataContext = vm;
        }
    }
}

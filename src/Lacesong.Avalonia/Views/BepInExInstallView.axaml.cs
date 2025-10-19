using System;
using Avalonia.Controls;
using Lacesong.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Lacesong.Avalonia.Views;

public partial class BepInExInstallView : UserControl
{
    public BepInExInstallView()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
        {
            var app = (App?)App.Current ?? throw new InvalidOperationException("App.Current is null");
            DataContext = app.Services.GetRequiredService<BepInExInstallViewModel>();
        }
    }
}

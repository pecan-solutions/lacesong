using Avalonia.Controls;
using Lacesong.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Lacesong.Avalonia.Views;

public partial class GameDetectionView : UserControl
{
    public GameDetectionView()
    {
        InitializeComponent();

        // This is a temporary solution to provide a ViewModel for the view during development.
        // In a real application, you would use a proper dependency injection framework
        // to resolve and assign the ViewModel.
        if (Design.IsDesignMode)
        {
            // In design mode, you can create a mock ViewModel
            // var mockServiceProvider = new ServiceCollection()
            //     .AddSingleton<IGameDetector, MockGameDetector>()
            //     .AddLogging()
            //     .BuildServiceProvider();
            // 
            // var logger = mockServiceProvider.GetRequiredService<ILogger<GameDetectionViewModel>>();
            // var gameDetector = mockServiceProvider.GetRequiredService<IGameDetector>();
            // 
            // DataContext = new GameDetectionViewModel(logger, gameDetector);
        }
        else
        {
            // At runtime, you would get the ViewModel from your service provider
            // This assumes you have an App.axaml.cs where you configure services.
            var app = (App?)App.Current ?? throw new InvalidOperationException("App.Current is null");
            DataContext = app.Services.GetRequiredService<GameDetectionViewModel>();
        }
    }
}

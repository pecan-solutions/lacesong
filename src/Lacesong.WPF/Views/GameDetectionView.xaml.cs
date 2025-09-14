using Lacesong.WPF.ViewModels;
using System.Windows.Controls;

namespace Lacesong.WPF.Views;

/// <summary>
/// game detection view
/// </summary>
public partial class GameDetectionView : UserControl
{
    public GameDetectionView(GameDetectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

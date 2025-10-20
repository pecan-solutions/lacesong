using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Lacesong.Avalonia.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        
        // prevent mouse wheel from changing combobox selection by intercepting the event in the tunnelling phase
        ThemeComboBox.AddHandler(InputElement.PointerWheelChangedEvent,
            (sender, e) => e.Handled = true,
            RoutingStrategies.Tunnel);
    }
}

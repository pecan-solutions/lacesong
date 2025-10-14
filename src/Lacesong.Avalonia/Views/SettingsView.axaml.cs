using Avalonia.Controls;
using Avalonia.Input;

namespace Lacesong.Avalonia.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        
        // prevent mouse wheel from changing combobox selection
        ThemeComboBox.PointerWheelChanged += (sender, e) => e.Handled = true;
    }
}

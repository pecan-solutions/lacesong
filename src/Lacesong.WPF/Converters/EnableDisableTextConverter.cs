using System;
using System.Globalization;
using System.Windows.Data;

namespace Lacesong.WPF.Converters;

/// <summary>
/// converts enabled status to enable/disable text
/// </summary>
public class EnableDisableTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? "Disable" : "Enable";
        }
        return "Enable";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

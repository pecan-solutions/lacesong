using System;
using System.Globalization;
using System.Windows.Data;

namespace Lacesong.WPF.Converters;

/// <summary>
/// converts enabled status to string
/// </summary>
public class EnabledStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? "Enabled" : "Disabled";
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Lacesong.WPF.Converters;

/// <summary>
/// converts mod status to color
/// </summary>
public class ModStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? Brushes.Green : Brushes.Orange;
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

using System;
using System.Globalization;
using System.Windows.Data;

namespace Lacesong.WPF.Converters;

/// <summary>
/// converts game status to string
/// </summary>
public class GameStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isDetected)
        {
            return isDetected ? "Detected" : "Not Detected";
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

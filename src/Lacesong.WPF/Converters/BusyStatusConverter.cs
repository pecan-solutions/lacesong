using System;
using System.Globalization;
using System.Windows.Data;

namespace Lacesong.WPF.Converters;

/// <summary>
/// converts busy status to string
/// </summary>
public class BusyStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isBusy)
        {
            return isBusy ? "Working..." : "Ready";
        }
        return "Ready";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

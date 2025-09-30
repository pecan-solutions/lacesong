using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Lacesong.Avalonia.Converters;

public class EnableDisableTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? "Disable" : "Enable";
        }
        return "Enable";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

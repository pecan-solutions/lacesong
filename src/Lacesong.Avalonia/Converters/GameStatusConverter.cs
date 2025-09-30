using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Lacesong.Avalonia.Converters;

public class GameStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isDetected)
        {
            return isDetected ? "Detected" : "Not Detected";
        }
        return "Unknown";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

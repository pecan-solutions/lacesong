using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Lacesong.Avalonia.Converters;

public class ModStatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? Brushes.Green : Brushes.Orange;
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

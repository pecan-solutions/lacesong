using System;
using Avalonia.Data.Converters;
using Avalonia;

namespace Lacesong.Avalonia.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
            return b ? Avalonia.Controls.Visibility.Visible : Avalonia.Controls.Visibility.Collapsed;
        return Avalonia.Controls.Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is Avalonia.Controls.Visibility v)
            return v == Avalonia.Controls.Visibility.Visible;
        return false;
    }
}

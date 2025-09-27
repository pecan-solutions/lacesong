using System;
using Avalonia.Data.Converters;
using Avalonia;

namespace Lacesong.Avalonia.Converters;

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
            return b ? Avalonia.Controls.Visibility.Collapsed : Avalonia.Controls.Visibility.Visible;
        return Avalonia.Controls.Visibility.Visible;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is Avalonia.Controls.Visibility v)
            return v != Avalonia.Controls.Visibility.Visible;
        return true;
    }
}

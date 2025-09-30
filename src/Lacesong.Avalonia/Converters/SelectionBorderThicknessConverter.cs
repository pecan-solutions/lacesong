using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Lacesong.Avalonia.Converters;

public class SelectionBorderThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
        {
            return isSelected ? new Thickness(2) : new Thickness(1);
        }
        return new Thickness(1);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

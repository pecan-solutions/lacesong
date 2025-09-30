using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Lacesong.Avalonia.Converters;

public class ActiveViewConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return string.Empty;

        return value.GetType() == (Type)parameter ? "Active" : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

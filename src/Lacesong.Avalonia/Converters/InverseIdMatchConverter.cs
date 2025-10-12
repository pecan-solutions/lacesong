using System;
using System.Globalization;
using Avalonia.Data.Converters;
using System.Collections.Generic;

namespace Lacesong.Avalonia.Converters;

public class InverseIdMatchConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return true;
        var modId = values[0]?.ToString();
        var installingId = values[1]?.ToString();
        // return true (enabled) when NOT installing this mod
        return string.IsNullOrEmpty(installingId) || installingId != modId;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("one-way conversion only");
    }
}


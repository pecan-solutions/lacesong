using System;
using System.Globalization;
using Avalonia.Data.Converters;
using System.Collections.Generic;

namespace Lacesong.Avalonia.Converters;

public class IdMatchConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return false;
        var modId = values[0]?.ToString();
        var installingId = values[1]?.ToString();
        return !string.IsNullOrEmpty(installingId) && installingId == modId;
    }
}

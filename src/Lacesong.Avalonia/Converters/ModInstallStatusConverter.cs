using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lacesong.Avalonia.Converters;

public class ModInstallStatusConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] == null || values[1] == null)
            return false;

        var modName = values[0]?.ToString();
        var installedModNames = values[1] as HashSet<string>;

        if (string.IsNullOrEmpty(modName) || installedModNames == null)
            return false;

        return installedModNames.Contains(modName);
    }

    public object[] ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

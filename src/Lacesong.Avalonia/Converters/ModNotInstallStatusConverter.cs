using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lacesong.Avalonia.Converters;

public class ModNotInstallStatusConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] == null || values[1] == null)
            return true; // show install button if we can't determine status

        var modName = values[0]?.ToString();
        var installedModNames = values[1] as HashSet<string>;

        if (string.IsNullOrEmpty(modName) || installedModNames == null)
            return true; // show install button if we can't determine status

        return !installedModNames.Contains(modName); // return true if NOT installed (show install button)
    }

    public object[] ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

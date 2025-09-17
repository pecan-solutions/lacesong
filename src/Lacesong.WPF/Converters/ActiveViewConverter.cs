using System;
using System.Globalization;
using System.Windows.Data;

namespace Lacesong.WPF.Converters;

public class ActiveViewConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return null;

        return value.GetType() == (Type)parameter ? "Active" : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

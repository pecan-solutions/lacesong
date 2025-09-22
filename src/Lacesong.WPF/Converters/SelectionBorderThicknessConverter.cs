using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Lacesong.WPF.Converters;

/// <summary>
/// converts selection status to border thickness
/// </summary>
public class SelectionBorderThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
        {
            return isSelected ? new Thickness(2) : new Thickness(1);
        }
        return new Thickness(1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

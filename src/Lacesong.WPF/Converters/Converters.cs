using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Lacesong.WPF.Converters;

/// <summary>
/// converter for boolean to visibility
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue && boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Visible;
    }
}

/// <summary>
/// converter for inverse boolean to visibility
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue && !boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Collapsed;
    }
}

/// <summary>
/// converter for inverse boolean
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue ? !boolValue : false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue ? !boolValue : false;
    }
}

/// <summary>
/// converter for count to visibility
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// converter for busy status
/// </summary>
public class BusyStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isBusy ? (isBusy ? "Working..." : "Ready") : "Ready";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// converter for bepinex status
/// </summary>
public class BepInExStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isInstalled ? (isInstalled ? "Installed" : "Not Installed") : "Not Installed";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// converter for enabled status color
/// </summary>
public class EnabledStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isEnabled ? 
            (isEnabled ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Orange)) : 
            new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// converter for mod status text
/// </summary>
public class ModStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isEnabled ? (isEnabled ? "Enabled" : "Disabled") : "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// converter for mod status color
/// </summary>
public class ModStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isEnabled ? 
            (isEnabled ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Orange)) : 
            new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// converter for game status text
/// </summary>
public class GameStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isDetected ? (isDetected ? "Game detected" : "No game detected") : "No game detected";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// converts compatibility status enum to color brush
/// </summary>
public class CompatibilityStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Lacesong.Core.Models.CompatibilityStatus status)
        {
            return status switch
            {
                Lacesong.Core.Models.CompatibilityStatus.Compatible => new SolidColorBrush(Colors.Green),
                Lacesong.Core.Models.CompatibilityStatus.CompatibleWithIssues => new SolidColorBrush(Colors.Orange),
                Lacesong.Core.Models.CompatibilityStatus.Incompatible => new SolidColorBrush(Colors.Red),
                Lacesong.Core.Models.CompatibilityStatus.Deprecated => new SolidColorBrush(Colors.Gray),
                _ => new SolidColorBrush(Colors.LightGray)
            };
        }
        return new SolidColorBrush(Colors.LightGray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// converts compatibility status enum to short text
/// </summary>
public class CompatibilityStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Lacesong.Core.Models.CompatibilityStatus status)
        {
            return status switch
            {
                Lacesong.Core.Models.CompatibilityStatus.Compatible => "OK",
                Lacesong.Core.Models.CompatibilityStatus.CompatibleWithIssues => "!!",
                Lacesong.Core.Models.CompatibilityStatus.Incompatible => "X",
                Lacesong.Core.Models.CompatibilityStatus.Deprecated => "DEP",
                _ => "?"
            };
        }
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
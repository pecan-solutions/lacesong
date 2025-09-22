using System;
using System.Globalization;
using System.Windows.Data;

namespace Lacesong.WPF.Converters;

/// <summary>
/// converts a DateTime to a human friendly relative time string like "a month ago".
/// </summary>
public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return string.Empty;
        var span = DateTime.UtcNow - dt.ToUniversalTime();
        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minute(s) ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hour(s) ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays} day(s) ago";
        if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)} month(s) ago";
        return $"{(int)(span.TotalDays / 365)} year(s) ago";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

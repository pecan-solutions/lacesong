using System;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Lacesong.Avalonia.Converters;

public class RelativeTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime) return null;
        var span = DateTime.UtcNow - dateTime.ToUniversalTime();
        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minutes ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays} days ago";
        if (span.TotalDays < 365) return $"{(int)(span.TotalDays/30)} months ago";
        return $"{(int)(span.TotalDays/365)} years ago";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

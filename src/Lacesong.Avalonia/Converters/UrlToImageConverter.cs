using Avalonia.Data.Converters;
using Avalonia.Threading;
using Lacesong.Avalonia.Services;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.Converters;

public class UrlToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url))
            return null;

        // for local file paths or avalonia resource paths
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // return a task that avalonia can await
        // avalonia's image control supports Task<> sources
        return LoadImageAsync(url);
    }

    private async Task<object?> LoadImageAsync(string url)
    {
        return await ImageCacheService.Instance.LoadImageAsync(url);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


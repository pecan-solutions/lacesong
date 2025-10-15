using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Globalization;
using System.IO;

namespace Lacesong.Avalonia.Converters;

/// <summary>
/// converts a local file path to an image bitmap, with fallback to placeholder
/// </summary>
public class LocalImageConverter : IValueConverter
{
    private static Bitmap? _placeholderImage;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                // try to load from local file
                if (File.Exists(path))
                {
                    return new Bitmap(path);
                }
            }
            catch
            {
                // fall through to placeholder
            }
        }

        // return placeholder
        return GetPlaceholder();
    }

    private static Bitmap? GetPlaceholder()
    {
        if (_placeholderImage != null)
            return _placeholderImage;

        try
        {
            var assets = AssetLoader.Open(new Uri("avares://Lacesong.Avalonia/Assets/placeholderimage.png"));
            _placeholderImage = new Bitmap(assets);
            return _placeholderImage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load placeholder image: {ex.Message}");
            // try alternative path
            try
            {
                var altAssets = AssetLoader.Open(new Uri("avares://Lacesong.Avalonia/Assets/placeholder.png"));
                _placeholderImage = new Bitmap(altAssets);
                return _placeholderImage;
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"Failed to load alternative placeholder: {ex2.Message}");
                return null;
            }
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


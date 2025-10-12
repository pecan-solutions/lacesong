using Avalonia.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.Services;

public class ImageCacheService
{
    private static readonly ImageCacheService _instance = new();
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _cache;
    private readonly string _cacheDirectory;

    public static ImageCacheService Instance => _instance;

    private ImageCacheService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _cache = new ConcurrentDictionary<string, Task<Bitmap?>>();
        
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDirectory = Path.Combine(appDataPath, "Lacesong", "ImageCache");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<Bitmap?> LoadImageAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // return cached task if already loading/loaded
        var task = _cache.GetOrAdd(url, _ => LoadImageInternalAsync(url));
        var result = await task;
        
        // if the load failed, remove it from cache so we can retry later
        if (result == null)
        {
            _cache.TryRemove(url, out _);
        }
        
        return result;
    }
    
    private async Task<Bitmap?> LoadImageInternalAsync(string url)
    {
        try
        {
            // check disk cache first
            var cachedPath = GetCachedImagePath(url);
            if (File.Exists(cachedPath))
            {
                try
                {
                    return new Bitmap(cachedPath);
                }
                catch
                {
                    // corrupted cache file, delete it
                    File.Delete(cachedPath);
                }
            }

            // download from URL
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var imageData = await response.Content.ReadAsByteArrayAsync();
            
            // save to disk cache
            await File.WriteAllBytesAsync(cachedPath, imageData);
            
            // load from disk cache
            return new Bitmap(cachedPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"failed to load image from {url}: {ex.Message}");
            return null;
        }
    }

    public Bitmap? LoadImageSync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            // check if already in memory cache
            if (_cache.TryGetValue(url, out var cachedTask) && cachedTask.IsCompletedSuccessfully)
            {
                return cachedTask.Result;
            }

            // check disk cache
            var cachedPath = GetCachedImagePath(url);
            if (File.Exists(cachedPath))
            {
                try
                {
                    return new Bitmap(cachedPath);
                }
                catch
                {
                    // corrupted cache file, delete it
                    File.Delete(cachedPath);
                }
            }

            // start async load in background without blocking
            _ = LoadImageAsync(url);
            return null;
        }
        catch
        {
            return null;
        }
    }

    private string GetCachedImagePath(string url)
    {
        var hash = ComputeHash(url);
        return Path.Combine(_cacheDirectory, $"{hash}.cache");
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16];
    }

    public void ClearCache()
    {
        _cache.Clear();
        if (Directory.Exists(_cacheDirectory))
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // ignore errors
                }
            }
        }
    }
}


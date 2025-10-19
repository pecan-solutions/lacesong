using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Text.Json;

namespace Lacesong.Core.Services;

/// <summary>
/// service for caching bepinex version information to avoid frequent api calls
/// </summary>
public class BepInExVersionCacheService : IBepInExVersionCacheService
{
    private readonly HttpClient _httpClient;
    private BepInExVersionCache? _cachedVersion;
    private readonly object _cacheLock = new object();
    
    // cache for 12 hours as bepinex releases are infrequent (every 3 weeks for stable)
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(12);
    private const string GithubLatestReleaseUrl = "https://api.github.com/repos/BepInEx/BepInEx/releases/latest";

    public BepInExVersionCacheService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Lacesong-ModManager/1.0.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string?> GetLatestVersionAsync()
    {
        lock (_cacheLock)
        {
            // check if we have valid cached data
            if (_cachedVersion != null && !IsCacheExpired(_cachedVersion))
            {
                return _cachedVersion.Version;
            }
        }

        // fetch fresh data from api
        return await FetchLatestVersionFromApiAsync();
    }

    public async Task<BepInExVersionInfo?> GetLatestVersionInfoAsync()
    {
        lock (_cacheLock)
        {
            // check if we have valid cached data
            if (_cachedVersion != null && !IsCacheExpired(_cachedVersion))
            {
                return _cachedVersion.VersionInfo;
            }
        }

        // fetch fresh data from api
        return await FetchLatestVersionInfoFromApiAsync();
    }

    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedVersion = null;
        }
    }

    public bool IsCacheValid()
    {
        lock (_cacheLock)
        {
            return _cachedVersion != null && !IsCacheExpired(_cachedVersion);
        }
    }

    private async Task<string?> FetchLatestVersionFromApiAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(GithubLatestReleaseUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to fetch latest BepInEx version from GitHub API. Status: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            
            if (document.RootElement.TryGetProperty("tag_name", out var tagNameElement))
            {
                var tagName = tagNameElement.GetString();
                if (!string.IsNullOrEmpty(tagName))
                {
                    // remove 'v' prefix if present (e.g., "v5.4.23.4" -> "5.4.23.4")
                    var version = tagName.TrimStart('v');
                    
                    // cache the result
                    lock (_cacheLock)
                    {
                        _cachedVersion = new BepInExVersionCache
                        {
                            Version = version,
                            VersionInfo = ExtractVersionInfo(document.RootElement),
                            CachedAt = DateTime.UtcNow
                        };
                    }
                    
                    Console.WriteLine($"Successfully fetched and cached latest BepInEx version: {version}");
                    return version;
                }
            }
            
            Console.WriteLine("No tag_name found in GitHub API response");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching latest BepInEx version from GitHub API: {ex.Message}");
            return null;
        }
    }

    private async Task<BepInExVersionInfo?> FetchLatestVersionInfoFromApiAsync()
    {
        try
        {
            Console.WriteLine("Fetching latest BepInEx version info from GitHub API");
            
            using var response = await _httpClient.GetAsync(GithubLatestReleaseUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to fetch latest BepInEx version info from GitHub API. Status: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            
            var versionInfo = ExtractVersionInfo(document.RootElement);
            if (versionInfo != null)
            {
                // cache the result
                lock (_cacheLock)
                {
                    _cachedVersion = new BepInExVersionCache
                    {
                        Version = versionInfo.FileVersion ?? string.Empty,
                        VersionInfo = versionInfo,
                        CachedAt = DateTime.UtcNow
                    };
                }
                
                Console.WriteLine($"Successfully fetched and cached latest BepInEx version info: {versionInfo.FileVersion}");
            }
            
            return versionInfo;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching latest BepInEx version info from GitHub API: {ex.Message}");
            return null;
        }
    }

    private static BepInExVersionInfo? ExtractVersionInfo(JsonElement rootElement)
    {
        try
        {
            var versionInfo = new BepInExVersionInfo();
            
            // extract basic version info
            if (rootElement.TryGetProperty("tag_name", out var tagNameElement))
            {
                var tagName = tagNameElement.GetString();
                if (!string.IsNullOrEmpty(tagName))
                {
                    versionInfo.FileVersion = tagName.TrimStart('v');
                    versionInfo.ProductVersion = tagName.TrimStart('v');
                }
            }
            
            // extract release information
            if (rootElement.TryGetProperty("published_at", out var publishedAtElement))
            {
                if (DateTime.TryParse(publishedAtElement.GetString(), out var publishedAt))
                {
                    // store published date in a custom property if needed
                }
            }
            
            if (rootElement.TryGetProperty("body", out var bodyElement))
            {
                // store release notes in description
                versionInfo.Description = bodyElement.GetString() ?? string.Empty;
            }
            
            if (rootElement.TryGetProperty("prerelease", out var prereleaseElement))
            {
                // we could store this in a custom property if needed
            }
            
            // set default values
            versionInfo.ProductName = "BepInEx";
            versionInfo.CompanyName = "BepInEx Team";
            
            return versionInfo;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsCacheExpired(BepInExVersionCache cache)
    {
        return DateTime.UtcNow - cache.CachedAt > CacheExpiration;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// internal cache structure for bepinex version data
/// </summary>
internal class BepInExVersionCache
{
    public string Version { get; set; } = string.Empty;
    public BepInExVersionInfo? VersionInfo { get; set; }
    public DateTime CachedAt { get; set; }
}

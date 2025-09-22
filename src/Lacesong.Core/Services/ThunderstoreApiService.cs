using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Lacesong.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Lacesong.Core.Services;

/// <summary>
/// provides methods to interact with the thunderstore api for hollow knight silksong.
/// </summary>
public class ThunderstoreApiService
{
    private const string BaseUrl = "https://thunderstore.io/c/hollow-knight-silksong/api/v1/";
    private readonly HttpClient _http;
    private readonly ILogger<ThunderstoreApiService>? _logger;
    private readonly IMemoryCache _cache;
    // cache configuration
    private readonly MemoryCacheEntryOptions _packageListOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        SlidingExpiration = TimeSpan.FromMinutes(10),
        Size = 1 // count as one unit regardless of list length
    };

    private readonly MemoryCacheEntryOptions _packageDetailOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60),
        SlidingExpiration = TimeSpan.FromMinutes(15),
        Size = 1 // each detail costs one unit
    };

    public ThunderstoreApiService(HttpClient? httpClient = null,
        ILogger<ThunderstoreApiService>? logger = null,
        IMemoryCache? memoryCache = null)
    {
        _http = httpClient ?? new HttpClient();
        _logger = logger;
        _cache = memoryCache ?? new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 128 // allow up to 128 units (~128 lists/details) to keep memory bounded
        });

        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LacesongModManager", "1.0"));
    }

    /// <summary>
    /// fetches all packages from thunderstore. caches for a short duration to avoid spamming the api.
    /// </summary>
    public async Task<IReadOnlyList<ThunderstorePackage>> GetPackagesAsync(bool forceRefresh = false, CancellationToken token = default)
    {
        const string cacheKey = "thunderstore_packages";
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<ThunderstorePackage> cachedList))
        {
            return cachedList;
        }

        try
        {
            var url = $"{BaseUrl}package/";
            var result = await _http.GetFromJsonAsync<List<ThunderstorePackage>>(url, token) ?? new List<ThunderstorePackage>();
            _cache.Set(cacheKey, result, _packageListOptions);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "failed to fetch thunderstore packages");
            return _cache.TryGetValue(cacheKey, out List<ThunderstorePackage> fallback) ? fallback : new List<ThunderstorePackage>();
        }
    }

    /// <summary>
    /// fetches a single package by its full name (owner-name).
    /// </summary>
    public async Task<ThunderstorePackage?> GetPackageAsync(string fullName, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return null;

        var cacheKey = $"ts_pkg_{fullName.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out ThunderstorePackage cached))
        {
            return cached;
        }

        var url = $"{BaseUrl}package/{fullName}/";
        try
        {
            var package = await _http.GetFromJsonAsync<ThunderstorePackage>(url, token);
            if (package != null)
            {
                _cache.Set(cacheKey, package, _packageDetailOptions);
            }
            return package;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "failed to fetch thunderstore package {Package}", fullName);
            return null;
        }
    }
}

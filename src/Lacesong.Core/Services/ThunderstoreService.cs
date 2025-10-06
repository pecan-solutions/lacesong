using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Lacesong.Core.Models;

namespace Lacesong.Core.Services;

public class ThunderstoreService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _throttle = new(1, 1);
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(15);
    private readonly string _baseUrl;

    public ThunderstoreService(HttpClient? http = null, IMemoryCache? cache = null, string? baseUrl = null)
    {
        _http = http ?? new HttpClient();
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
        _baseUrl = baseUrl ?? Environment.GetEnvironmentVariable("THUNDERSTORE_BASE_URL")?.TrimEnd('/') ?? "https://thunderstore.io";
    }

    public async Task<IReadOnlyList<ThunderstorePackageDto>> GetPackagesAsync(int page = 1, CancellationToken token = default)
    {
        var cacheKey = $"packages_page_{page}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<ThunderstorePackageDto> cached))
        {
            Console.WriteLine($"ThunderstoreService: GetPackagesAsync - Returning {cached.Count} cached packages for page {page}");
            return cached;
        }

        Console.WriteLine($"ThunderstoreService: GetPackagesAsync - Fetching page {page} from API");
        await _throttle.WaitAsync(token);
        try
        {
            // double check after waiting
            if (_cache.TryGetValue(cacheKey, out cached))
            {
                Console.WriteLine($"ThunderstoreService: GetPackagesAsync - Found cached data after waiting for page {page}");
                return cached;
            }

            var url = $"{_baseUrl}/c/hollow-knight-silksong/api/v1/package/?page={page}";
            Console.WriteLine($"ThunderstoreService: GetPackagesAsync - Making request to: {url}");
            var response = await _http.GetAsync(url, token);
            Console.WriteLine($"ThunderstoreService: GetPackagesAsync - Response status: {response.StatusCode}");
            response.EnsureSuccessStatusCode();
            var packages = await response.Content.ReadFromJsonAsync<List<ThunderstorePackageDto>>(cancellationToken: token) ?? new();
            Console.WriteLine($"ThunderstoreService: GetPackagesAsync - Deserialized {packages.Count} packages for page {page}");
            _cache.Set(cacheKey, packages, _cacheTtl);
            return packages;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ThunderstoreService: GetPackagesAsync - Exception for page {page}: {ex.Message}");
            throw;
        }
        finally
        {
            _throttle.Release();
        }
    }
}

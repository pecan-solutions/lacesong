using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Lacesong.Core.Models;

namespace Lacesong.Core.Services;

public class ThunderstoreService : IDisposable
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _throttle = new(1, 1);
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(15);
    private readonly string _baseUrl;
    private readonly string _cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lacesong", "cache");
    private readonly bool _ownsHttpClient;
    private readonly bool _ownsCache;

    public ThunderstoreService(HttpClient? http = null, IMemoryCache? cache = null, string? baseUrl = null)
    {
        _ownsHttpClient = http == null;
        _ownsCache = cache == null;
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

    public async Task<ThunderstorePackageDetailDto?> GetPackageDetailAsync(string ns, string name, bool force = false, CancellationToken token = default)
    {
        var cacheKey = $"pkg_{ns}_{name}";
        if (!force && _cache.TryGetValue(cacheKey, out ThunderstorePackageDetailDto cachedDto))
        {
            return cachedDto;
        }

        // disk cache
        Directory.CreateDirectory(_cacheDir);
        var filePath = Path.Combine(_cacheDir, $"{cacheKey}.json");
        if (!force && File.Exists(filePath))
        {
            var info = new FileInfo(filePath);
            if (DateTime.UtcNow - info.LastWriteTimeUtc < TimeSpan.FromHours(24))
            {
                try
                {
                    var txt = await File.ReadAllTextAsync(filePath, token);
                    var dto = System.Text.Json.JsonSerializer.Deserialize<ThunderstorePackageDetailDto>(txt);
                    if (dto != null)
                    {
                        _cache.Set(cacheKey, dto, _cacheTtl);
                        return dto;
                    }
                }
                catch { /* ignore and refetch */ }
            }
        }

        var url = $"{_baseUrl}/api/experimental/package/{ns}/{name}/";
        try
        {
            var resp = await _http.GetAsync(url, token);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null; // mod not on thunderstore
            }
            resp.EnsureSuccessStatusCode();
            var dto = await resp.Content.ReadFromJsonAsync<ThunderstorePackageDetailDto>(cancellationToken: token);
            if (dto != null)
            {
                _cache.Set(cacheKey, dto, _cacheTtl);
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(dto);
                    await File.WriteAllTextAsync(filePath, json, token);
                }
                catch { /* ignore */ }
            }
            return dto;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _http?.Dispose();
        
        if (_ownsCache)
            _cache?.Dispose();
            
        _throttle?.Dispose();
    }
}

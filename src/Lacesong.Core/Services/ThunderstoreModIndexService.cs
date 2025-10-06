using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Lacesong.Core.Services;

public class ThunderstoreModIndexService : IModIndexService
{
    private readonly ThunderstoreService _api;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(15);

    public ThunderstoreModIndexService(ThunderstoreService api, IMemoryCache? cache = null)
    {
        _api = api;
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
    }

    private async Task<List<ThunderstorePackageDto>> GetAllPackages(CancellationToken token = default)
    {
        const string key = "all_packages";
        if (_cache.TryGetValue(key, out List<ThunderstorePackageDto> cached))
            return cached;

        var page = 1;
        var all = new List<ThunderstorePackageDto>();
        while (true)
        {
            var pageData = await _api.GetPackagesAsync(page, token);
            if (pageData.Count == 0) break;
            all.AddRange(pageData);
            page++;
        }
        _cache.Set(key, all, _ttl);
        return all;
    }

    private static ModIndexEntry MapPackage(ThunderstorePackageDto pkg)
    {
        var latest = pkg.Versions.OrderByDescending(v => v.DateCreated).FirstOrDefault();
        return new ModIndexEntry
        {
            Id = pkg.FullName,
            Name = pkg.Name,
            Description = latest?.Description ?? string.Empty,
            Author = pkg.Owner,
            Category = pkg.Categories.FirstOrDefault() ?? "General",
            Tags = pkg.Categories,
            DownloadCount = latest?.Downloads ?? 0,
            Rating = pkg.RatingScore,
            LastUpdated = pkg.DateUpdated,
            Versions = pkg.Versions.Select(v => new ModVersion
            {
                Version = v.VersionNumber,
                DownloadUrl = v.DownloadUrl,
                FileName = System.IO.Path.GetFileName(new Uri(v.DownloadUrl).AbsolutePath),
                FileSize = v.FileSize,
                ReleaseDate = v.DateCreated,
                Changelog = null,
                IsPrerelease = false
            }).ToList()
        };
    }

    public async Task<ModIndex?> FetchModIndex(string repositoryUrl)
    {
        // thunderstore does not provide single index file; return null
        return null;
    }

    public async Task<ModSearchResults> SearchMods(ModSearchCriteria criteria)
    {
        var packages = await GetAllPackages();
        IEnumerable<ThunderstorePackageDto> query = packages;
        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var q = criteria.Query.ToLowerInvariant();
            query = query.Where(p => p.Name.ToLowerInvariant().Contains(q) || p.Owner.ToLowerInvariant().Contains(q));
        }
        if (!string.IsNullOrWhiteSpace(criteria.Category))
        {
            query = query.Where(p => p.Categories.Contains(criteria.Category));
        }
        // sorting
        query = criteria.SortBy switch
        {
            "downloads" => criteria.SortOrder == "desc" ? query.OrderByDescending(p => p.Versions.First().Downloads) : query.OrderBy(p => p.Versions.First().Downloads),
            "rating" => criteria.SortOrder == "desc" ? query.OrderByDescending(p => p.RatingScore) : query.OrderBy(p => p.RatingScore),
            "date" => criteria.SortOrder == "desc" ? query.OrderByDescending(p => p.DateUpdated) : query.OrderBy(p => p.DateUpdated),
            _ => criteria.SortOrder == "desc" ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name)
        };

        var total = query.Count();
        var pageSize = criteria.PageSize <= 0 ? 20 : criteria.PageSize;
        var page = criteria.Page <= 0 ? 1 : criteria.Page;
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var results = new ModSearchResults
        {
            Mods = paged.Select(MapPackage).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            SearchTime = TimeSpan.Zero
        };
        return results;
    }

    public async Task<ModIndexEntry?> GetMod(string modId)
    {
        var packages = await GetAllPackages();
        var pkg = packages.FirstOrDefault(p => p.FullName.Equals(modId, StringComparison.OrdinalIgnoreCase));
        return pkg == null ? null : MapPackage(pkg);
    }

    public async Task<List<string>> GetCategories()
    {
        var packages = await GetAllPackages();
        return packages.SelectMany(p => p.Categories).Distinct().OrderBy(c => c).ToList();
    }

    public Task<OperationResult> RefreshIndex()
    {
        _cache.Remove("all_packages");
        return Task.FromResult(OperationResult.SuccessResult("Cache cleared"));
    }

    public Task<OperationResult> AddRepository(ModRepository repository) => Task.FromResult(OperationResult.ErrorResult("Not supported"));
    public Task<OperationResult> RemoveRepository(string repositoryId) => Task.FromResult(OperationResult.ErrorResult("Not supported"));
    public Task<List<ModRepository>> GetRepositories() => Task.FromResult(new List<ModRepository>());
}

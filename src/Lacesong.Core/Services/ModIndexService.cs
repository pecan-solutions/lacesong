using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Lacesong.Core.Services;

/// <summary>
/// service for managing mod index fetching, caching, and searching
/// </summary>
public class ModIndexService : IModIndexService
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly string _repositoriesConfigPath;
    private ModIndex? _cachedIndex;
    private List<ModRepository> _repositories = new();

    public ModIndexService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        
        // set up cache directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDirectory = Path.Combine(appDataPath, "Lacesong", "Cache");
        Directory.CreateDirectory(_cacheDirectory);
        
        _repositoriesConfigPath = Path.Combine(_cacheDirectory, "repositories.json");
        
        // load default repositories
        LoadDefaultRepositories();
        LoadRepositories();
    }

    public async Task<ModIndex?> FetchModIndex(string repositoryUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync(repositoryUrl);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            try
            {
                var modIndex = JsonSerializer.Deserialize<ModIndex>(json);
                if (modIndex != null)
                {
                    // cache the index
                    await CacheModIndex(modIndex, repositoryUrl);
                }
                return modIndex;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error deserializing mod index from {repositoryUrl}: {ex.Message}");
                Console.WriteLine($"Problematic JSON: {json}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching mod index from {repositoryUrl}: {ex.Message}");
            return null;
        }
    }

    public async Task<ModSearchResults> SearchMods(ModSearchCriteria criteria)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // ensure we have a cached index
            if (_cachedIndex == null)
            {
                await RefreshIndex();
            }
            
            if (_cachedIndex == null)
            {
                return new ModSearchResults
                {
                    Mods = new List<ModIndexEntry>(),
                    TotalCount = 0,
                    Page = criteria.Page,
                    PageSize = criteria.PageSize,
                    TotalPages = 0,
                    SearchTime = DateTime.UtcNow - startTime
                };
            }
            
            var results = _cachedIndex.Mods.AsEnumerable();
            
            // apply filters
            if (!string.IsNullOrEmpty(criteria.Query))
            {
                var query = criteria.Query.ToLowerInvariant();
                results = results.Where(m => 
                    m.Name.ToLowerInvariant().Contains(query) ||
                    m.Description.ToLowerInvariant().Contains(query) ||
                    m.Author.ToLowerInvariant().Contains(query) ||
                    m.Tags.Any(t => t.ToLowerInvariant().Contains(query)));
            }
            
            if (!string.IsNullOrEmpty(criteria.Category))
            {
                results = results.Where(m => m.Category.Equals(criteria.Category, StringComparison.OrdinalIgnoreCase));
            }
            
            if (!string.IsNullOrEmpty(criteria.Author))
            {
                results = results.Where(m => m.Author.Equals(criteria.Author, StringComparison.OrdinalIgnoreCase));
            }
            
            if (criteria.Tags.Count > 0)
            {
                results = results.Where(m => criteria.Tags.Any(tag => 
                    m.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase))));
            }
            
            if (criteria.IsOfficial.HasValue)
            {
                results = results.Where(m => m.IsOfficial == criteria.IsOfficial.Value);
            }
            
            if (criteria.IsVerified.HasValue)
            {
                results = results.Where(m => m.IsVerified == criteria.IsVerified.Value);
            }
            
            if (criteria.GameCompatibility.Count > 0)
            {
                results = results.Where(m => 
                    criteria.GameCompatibility.Any(game => 
                        m.GameCompatibility.Any(gc => gc.Equals(game, StringComparison.OrdinalIgnoreCase))));
            }
            
            // apply sorting
            results = criteria.SortBy.ToLowerInvariant() switch
            {
                "name" => criteria.SortOrder == "desc" ? results.OrderByDescending(m => m.Name) : results.OrderBy(m => m.Name),
                "date" => criteria.SortOrder == "desc" ? results.OrderByDescending(m => m.LastUpdated) : results.OrderBy(m => m.LastUpdated),
                "downloads" => criteria.SortOrder == "desc" ? results.OrderByDescending(m => m.DownloadCount) : results.OrderBy(m => m.DownloadCount),
                "rating" => criteria.SortOrder == "desc" ? results.OrderByDescending(m => m.Rating) : results.OrderBy(m => m.Rating),
                _ => results.OrderBy(m => m.Name)
            };
            
            var totalCount = results.Count();
            var totalPages = (int)Math.Ceiling((double)totalCount / criteria.PageSize);
            
            // apply pagination
            var pagedResults = results
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .ToList();
            
            return new ModSearchResults
            {
                Mods = pagedResults,
                TotalCount = totalCount,
                Page = criteria.Page,
                PageSize = criteria.PageSize,
                TotalPages = totalPages,
                SearchTime = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching mods: {ex.Message}");
            return new ModSearchResults
            {
                Mods = new List<ModIndexEntry>(),
                TotalCount = 0,
                Page = criteria.Page,
                PageSize = criteria.PageSize,
                TotalPages = 0,
                SearchTime = DateTime.UtcNow - startTime
            };
        }
    }

    public async Task<ModIndexEntry?> GetMod(string modId)
    {
        try
        {
            if (_cachedIndex == null)
            {
                await RefreshIndex();
            }
            
            return _cachedIndex?.Mods.FirstOrDefault(m => m.Id.Equals(modId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting mod {modId}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<string>> GetCategories()
    {
        try
        {
            if (_cachedIndex == null)
            {
                await RefreshIndex();
            }
            
            return _cachedIndex?.Categories ?? new List<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting categories: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<OperationResult> RefreshIndex()
    {
        try
        {
            var allMods = new List<ModIndexEntry>();
            var allCategories = new HashSet<string>();
            
            foreach (var repository in _repositories.Where(r => r.IsEnabled))
            {
                try
                {
                    var index = await FetchModIndex(repository.Url);
                    if (index != null)
                    {
                        allMods.AddRange(index.Mods);
                        foreach (var category in index.Categories)
                        {
                            allCategories.Add(category);
                        }
                        
                        // update repository sync time
                        repository.LastSync = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error refreshing repository {repository.Name}: {ex.Message}");
                }
            }
            
            // create combined index
            _cachedIndex = new ModIndex
            {
                Version = "1.0.0",
                LastUpdated = DateTime.UtcNow,
                TotalMods = allMods.Count,
                Categories = allCategories.ToList(),
                Mods = allMods,
                Repositories = _repositories
            };
            
            // save combined index to cache
            await SaveCachedIndex();
            
            return OperationResult.SuccessResult($"Index refreshed successfully with {allMods.Count} mods from {_repositories.Count(r => r.IsEnabled)} repositories");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to refresh index");
        }
    }

    public async Task<OperationResult> AddRepository(ModRepository repository)
    {
        try
        {
            // validate repository
            if (string.IsNullOrEmpty(repository.Id) || string.IsNullOrEmpty(repository.Url))
            {
                return OperationResult.ErrorResult("Repository ID and URL are required", "Invalid repository configuration");
            }
            
            // check if repository already exists
            if (_repositories.Any(r => r.Id.Equals(repository.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return OperationResult.ErrorResult("Repository with this ID already exists", "Duplicate repository");
            }
            
            // validate repository url
            try
            {
                var testResponse = await _httpClient.GetAsync(repository.Url);
                if (!testResponse.IsSuccessStatusCode)
                {
                    return OperationResult.ErrorResult($"Repository URL is not accessible: {testResponse.StatusCode}", "Invalid repository URL");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.ErrorResult($"Cannot access repository URL: {ex.Message}", "Invalid repository URL");
            }
            
            _repositories.Add(repository);
            await SaveRepositories();
            
            return OperationResult.SuccessResult($"Repository '{repository.Name}' added successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to add repository");
        }
    }

    public async Task<OperationResult> RemoveRepository(string repositoryId)
    {
        try
        {
            var repository = _repositories.FirstOrDefault(r => r.Id.Equals(repositoryId, StringComparison.OrdinalIgnoreCase));
            if (repository == null)
            {
                return OperationResult.ErrorResult("Repository not found", "Repository does not exist");
            }
            
            _repositories.Remove(repository);
            await SaveRepositories();
            
            return OperationResult.SuccessResult($"Repository '{repository.Name}' removed successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to remove repository");
        }
    }

    public async Task<List<ModRepository>> GetRepositories()
    {
        return await Task.FromResult(_repositories);
    }

    private async Task CacheModIndex(ModIndex modIndex, string repositoryUrl)
    {
        try
        {
            var urlHash = ComputeHash(repositoryUrl);
            var cachePath = Path.Combine(_cacheDirectory, $"index_{urlHash}.json");
            
            var json = JsonSerializer.Serialize(modIndex, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cachePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error caching mod index: {ex.Message}");
        }
    }

    private async Task SaveCachedIndex()
    {
        try
        {
            if (_cachedIndex != null)
            {
                var cachePath = Path.Combine(_cacheDirectory, "combined_index.json");
                var json = JsonSerializer.Serialize(_cachedIndex, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(cachePath, json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving cached index: {ex.Message}");
        }
    }

    private async Task LoadCachedIndex()
    {
        try
        {
            var cachePath = Path.Combine(_cacheDirectory, "combined_index.json");
            if (File.Exists(cachePath))
            {
                var json = await File.ReadAllTextAsync(cachePath);
                try
                {
                    _cachedIndex = JsonSerializer.Deserialize<ModIndex>(json);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error deserializing cached index: {ex.Message}");
                    Console.WriteLine($"Problematic JSON: {json}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cached index: {ex.Message}");
        }
    }

    private async Task SaveRepositories()
    {
        try
        {
            var json = JsonSerializer.Serialize(_repositories, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_repositoriesConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving repositories: {ex.Message}");
        }
    }

    private void LoadRepositories()
    {
        try
        {
            if (File.Exists(_repositoriesConfigPath))
            {
                var json = File.ReadAllText(_repositoriesConfigPath);
                try
                {
                    var savedRepositories = JsonSerializer.Deserialize<List<ModRepository>>(json);
                    if (savedRepositories != null)
                    {
                        _repositories.AddRange(savedRepositories);
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error loading repositories: {ex.Message}");
                    Console.WriteLine($"Problematic JSON: {json}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading repositories: {ex.Message}");
        }
    }

    private void LoadDefaultRepositories()
    {
        // add default official repository
        _repositories.Add(new ModRepository
        {
            Id = "official",
            Name = "Official Lacesong Mod Index",
            Url = "https://lacesong.dev/mods.json",
            Type = "Custom",
            IsOfficial = true,
            IsEnabled = true,
            Description = "Official curated mod index for Hollow Knight: Silksong"
        });
        
        // add example github repository
        _repositories.Add(new ModRepository
        {
            Id = "community-github",
            Name = "Community GitHub Mods",
            Url = "https://raw.githubusercontent.com/lacesong-community/mods/main/index.json",
            Type = "GitHub",
            IsOfficial = false,
            IsEnabled = true,
            Description = "Community-maintained mod index from GitHub"
        });
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16]; // use first 16 characters
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

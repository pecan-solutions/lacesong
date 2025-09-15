using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Lacesong.Core.Services;

/// <summary>
/// service for integrating with github releases to discover and index mods
/// </summary>
public class GitHubReleasesService : IGitHubReleasesService
{
    private readonly HttpClient _httpClient;
    private readonly string _githubApiBase = "https://api.github.com";

    public GitHubReleasesService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        
        // set user agent for github api
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Lacesong-ModManager/1.0.0");
    }

    public async Task<List<ModIndexEntry>> ScanRepositoryForMods(string owner, string repo)
    {
        try
        {
            var mods = new List<ModIndexEntry>();
            
            // first check if this repository is likely to contain mods
            if (!await IsLikelyModRepository(owner, repo))
            {
                return mods;
            }
            
            // get all releases
            var releases = await GetReleases(owner, repo);
            if (releases == null || releases.Count == 0)
            {
                return mods;
            }
            
            // filter releases to only include those with mod assets
            var releasesWithMods = new List<GitHubRelease>();
            foreach (var release in releases)
            {
                // skip prereleases unless specifically requested
                if (release.IsPrerelease)
                {
                    continue;
                }
                
                var assets = await GetReleaseAssets(owner, repo, release.Id);
                var modAssets = assets.Where(a => IsModAsset(a)).ToList();
                
                if (modAssets.Count > 0)
                {
                    releasesWithMods.Add(release);
                }
            }
            
            // only process releases that have mod assets
            foreach (var release in releasesWithMods)
            {
                var assets = await GetReleaseAssets(owner, repo, release.Id);
                var modAssets = assets.Where(a => IsModAsset(a)).ToList();
                
                if (modAssets.Count > 0)
                {
                    var modEntry = await CreateModIndexEntry(owner, repo, release, modAssets);
                    if (modEntry != null)
                    {
                        mods.Add(modEntry);
                    }
                }
            }
            
            return mods;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning repository {owner}/{repo} for mods: {ex.Message}");
            return new List<ModIndexEntry>();
        }
    }

    private async Task<bool> IsLikelyModRepository(string owner, string repo)
    {
        try
        {
            // get repository info
            var repoInfo = await GetRepositoryInfo(owner, repo);
            if (repoInfo == null)
            {
                return false;
            }
            
            // check repository name patterns
            var modNamePatterns = new[]
            {
                @"mod",
                @"plugin",
                @"bepinex",
                @"hollow.*knight",
                @"silksong",
                @"unity.*mod",
                @"game.*mod"
            };
            
            var repoName = $"{owner}/{repo}".ToLowerInvariant();
            var hasModName = modNamePatterns.Any(pattern => Regex.IsMatch(repoName, pattern, RegexOptions.IgnoreCase));
            
            // check repository description
            var description = repoInfo.Description?.ToLowerInvariant() ?? "";
            var modDescKeywords = new[]
            {
                "mod", "plugin", "bepinex", "hollow knight", "silksong", "unity", "game modification",
                "modding", "mod loader", "game enhancement", "patch", "fix", "improvement"
            };
            
            var hasModDescription = modDescKeywords.Any(keyword => description.Contains(keyword));
            
            // check repository topics
            var topics = repoInfo.Topics.Select(t => t.ToLowerInvariant());
            var modTopics = new[] { "mod", "plugin", "bepinex", "hollow-knight", "silksong", "unity", "modding", "game-modification" };
            var hasModTopics = topics.Any(topic => modTopics.Contains(topic));
            
            // check if repository has recent releases (last 6 months)
            var releases = await GetReleases(owner, repo);
            var recentReleases = releases?.Where(r => r.PublishedAt > DateTime.UtcNow.AddMonths(-6)).Count() ?? 0;
            var hasRecentReleases = recentReleases > 0;
            
            // repository is likely a mod repository if it meets multiple criteria
            var score = 0;
            if (hasModName) score++;
            if (hasModDescription) score++;
            if (hasModTopics) score++;
            if (hasRecentReleases) score++;
            
            // require at least 2 criteria to be met
            return score >= 2;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking if {owner}/{repo} is likely a mod repository: {ex.Message}");
            return false;
        }
    }

    public async Task<List<GitHubReleaseAsset>> GetReleaseAssets(string owner, string repo, string releaseId)
    {
        try
        {
            var url = $"{_githubApiBase}/repos/{owner}/{repo}/releases/{releaseId}/assets";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                return new List<GitHubReleaseAsset>();
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var assets = JsonSerializer.Deserialize<List<GitHubReleaseAsset>>(json);
            
            return assets ?? new List<GitHubReleaseAsset>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting release assets for {owner}/{repo}/{releaseId}: {ex.Message}");
            return new List<GitHubReleaseAsset>();
        }
    }

    public async Task<bool> IsModRepository(string owner, string repo)
    {
        try
        {
            // first check if repository is likely to contain mods
            if (!await IsLikelyModRepository(owner, repo))
            {
                return false;
            }
            
            var releases = await GetReleases(owner, repo);
            if (releases == null || releases.Count == 0)
            {
                return false;
            }
            
            // check if any release has mod assets (limit to last 10 releases for performance)
            var recentReleases = releases.Take(10);
            foreach (var release in recentReleases)
            {
                var assets = await GetReleaseAssets(owner, repo, release.Id);
                if (assets.Any(a => IsModAsset(a)))
                {
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking if {owner}/{repo} is a mod repository: {ex.Message}");
            return false;
        }
    }

    private async Task<List<GitHubRelease>?> GetReleases(string owner, string repo)
    {
        try
        {
            var url = $"{_githubApiBase}/repos/{owner}/{repo}/releases";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json);
            
            return releases;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting releases for {owner}/{repo}: {ex.Message}");
            return null;
        }
    }

    private async Task<ModIndexEntry?> CreateModIndexEntry(string owner, string repo, GitHubRelease release, List<GitHubReleaseAsset> assets)
    {
        try
        {
            // get repository info for additional metadata
            var repoInfo = await GetRepositoryInfo(owner, repo);
            
            var modEntry = new ModIndexEntry
            {
                Id = $"{owner}-{repo}".ToLowerInvariant(),
                Name = repoInfo?.Name ?? repo,
                Description = repoInfo?.Description ?? release.Body ?? "No description available",
                Author = owner,
                Repository = $"https://github.com/{owner}/{repo}",
                Homepage = repoInfo?.Homepage,
                Category = InferCategory(repoInfo?.Description ?? release.Body ?? ""),
                Tags = InferTags(repoInfo?.Description ?? release.Body ?? "", repoInfo?.Topics ?? new List<string>()),
                GameCompatibility = new List<string> { "hollow-knight-silksong" },
                IsOfficial = false,
                IsVerified = false,
                LastUpdated = release.PublishedAt,
                DownloadCount = 0, // DownloadCount not available in current Octokit version
                Versions = new List<ModVersion>()
            };
            
            // create version entries for each release
            var version = new ModVersion
            {
                Version = release.TagName.TrimStart('v'), // remove 'v' prefix if present
                DownloadUrl = assets.FirstOrDefault()?.DownloadUrl ?? "",
                FileName = assets.FirstOrDefault()?.Name ?? "",
                FileSize = assets.FirstOrDefault()?.Size ?? 0,
                ReleaseDate = release.PublishedAt,
                Changelog = release.Body,
                IsPrerelease = release.IsPrerelease,
                Dependencies = new List<ModDependency>(),
                Conflicts = new List<string>()
            };
            
            modEntry.Versions.Add(version);
            
            return modEntry;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating mod index entry for {owner}/{repo}: {ex.Message}");
            return null;
        }
    }

    private async Task<GitHubRepositoryInfo?> GetRepositoryInfo(string owner, string repo)
    {
        try
        {
            var url = $"{_githubApiBase}/repos/{owner}/{repo}";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GitHubRepositoryInfo>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting repository info for {owner}/{repo}: {ex.Message}");
            return null;
        }
    }

    private static bool IsModAsset(GitHubReleaseAsset asset)
    {
        var fileName = asset.Name.ToLowerInvariant();
        var contentType = asset.ContentType.ToLowerInvariant();
        
        // exclude common non-mod file types
        var excludedExtensions = new[] { ".txt", ".md", ".json", ".xml", ".yml", ".yaml", ".cfg", ".ini", ".log", ".exe", ".msi", ".deb", ".rpm", ".dmg", ".pkg" };
        if (excludedExtensions.Any(ext => fileName.EndsWith(ext)))
        {
            return false;
        }
        
        // exclude files that are clearly not mods based on name patterns
        var excludedPatterns = new[]
        {
            @"readme",
            @"changelog",
            @"license",
            @"install",
            @"setup",
            @"uninstall",
            @"source",
            @"src",
            @"test",
            @"debug",
            @"release-notes",
            @"documentation",
            @"docs"
        };
        
        if (excludedPatterns.Any(pattern => fileName.Contains(pattern)))
        {
            return false;
        }
        
        // check for common mod file extensions with size validation
        var modExtensions = new[] { ".zip", ".dll", ".mod", ".pak", ".unitypackage", ".assetbundle" };
        if (modExtensions.Any(ext => fileName.EndsWith(ext)))
        {
            // additional validation for specific extensions
            if (fileName.EndsWith(".dll"))
            {
                // dll files should be reasonable size (not too small or too large)
                if (asset.Size < 1024 || asset.Size > 50 * 1024 * 1024) // 1KB to 50MB
                {
                    return false;
                }
                
                // exclude system dlls and common non-mod dlls
                var systemDlls = new[] { "system", "windows", "microsoft", "core", "framework", "runtime" };
                if (systemDlls.Any(sys => fileName.Contains(sys)))
                {
                    return false;
                }
            }
            
            if (fileName.EndsWith(".zip"))
            {
                // zip files should be reasonable size
                if (asset.Size < 1024 || asset.Size > 100 * 1024 * 1024) // 1KB to 100MB
                {
                    return false;
                }
            }
            
            return true;
        }
        
        // check for common mod naming patterns with more specific validation
        var modPatterns = new[]
        {
            @"mod.*\.zip$",
            @".*mod.*\.dll$",
            @"bepinex.*\.zip$",
            @".*plugin.*\.zip$",
            @".*plugin.*\.dll$",
            @"hollow.*knight.*\.zip$",
            @"silksong.*\.zip$",
            @".*patch.*\.zip$",
            @".*fix.*\.zip$",
            @".*enhancement.*\.zip$",
            @".*improvement.*\.zip$"
        };
        
        var isModPattern = modPatterns.Any(pattern => Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase));
        
        if (isModPattern)
        {
            // additional validation for pattern matches
            if (fileName.EndsWith(".zip") && asset.Size > 100 * 1024 * 1024) // 100MB limit for zip files
            {
                return false;
            }
            
            if (fileName.EndsWith(".dll") && (asset.Size < 1024 || asset.Size > 50 * 1024 * 1024))
            {
                return false;
            }
            
            return true;
        }
        
        // check content type for additional validation
        var validContentTypes = new[] { "application/zip", "application/x-zip-compressed", "application/octet-stream", "application/x-msdownload" };
        if (validContentTypes.Contains(contentType) && (fileName.EndsWith(".zip") || fileName.EndsWith(".dll")))
        {
            // size validation based on content type
            if (contentType.Contains("zip") && asset.Size > 100 * 1024 * 1024)
            {
                return false;
            }
            
            if (contentType.Contains("download") && fileName.EndsWith(".dll") && asset.Size > 50 * 1024 * 1024)
            {
                return false;
            }
            
            return true;
        }
        
        return false;
    }

    private static string InferCategory(string description)
    {
        var desc = description.ToLowerInvariant();
        
        if (desc.Contains("ui") || desc.Contains("interface") || desc.Contains("menu"))
            return "UI";
        if (desc.Contains("gameplay") || desc.Contains("mechanics"))
            return "Gameplay";
        if (desc.Contains("graphics") || desc.Contains("visual") || desc.Contains("texture"))
            return "Graphics";
        if (desc.Contains("audio") || desc.Contains("sound") || desc.Contains("music"))
            return "Audio";
        if (desc.Contains("utility") || desc.Contains("tool") || desc.Contains("helper"))
            return "Utility";
        if (desc.Contains("cheat") || desc.Contains("debug") || desc.Contains("developer"))
            return "Developer";
        
        return "General";
    }

    private static List<string> InferTags(string description, List<string> topics)
    {
        var tags = new HashSet<string>();
        
        // add github topics
        foreach (var topic in topics)
        {
            tags.Add(topic.ToLowerInvariant());
        }
        
        // infer tags from description
        var desc = description.ToLowerInvariant();
        var commonTags = new[]
        {
            "bepinex", "unity", "mod", "plugin", "hollow-knight", "silksong",
            "ui", "gameplay", "graphics", "audio", "utility", "cheat", "debug"
        };
        
        foreach (var tag in commonTags)
        {
            if (desc.Contains(tag))
            {
                tags.Add(tag);
            }
        }
        
        return tags.ToList();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// represents a github release
/// </summary>
public class GitHubRelease
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("prerelease")]
    public bool IsPrerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool IsDraft { get; set; }
}

/// <summary>
/// represents github repository information
/// </summary>
public class GitHubRepositoryInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyName("topics")]
    public List<string> Topics { get; set; } = new();

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("stargazers_count")]
    public int StarCount { get; set; }

    [JsonPropertyName("forks_count")]
    public int ForkCount { get; set; }
}

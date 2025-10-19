using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lacesong.Core.Services;

/// <summary>
/// service for checking mod compatibility with game versions and other mods
/// </summary>
public class CompatibilityService : ICompatibilityService
{
    private readonly IModIndexService _modIndexService;
    private readonly IModManager _modManager;
    private readonly Dictionary<string, ModCompatibility> _compatibilityCache = new();
    private readonly object _cacheLock = new object();

    public CompatibilityService(IModIndexService modIndexService, IModManager modManager)
    {
        _modIndexService = modIndexService;
        _modManager = modManager;
    }

    public async Task<ModCompatibility> CheckCompatibility(string modId, GameInstallation gameInstall)
    {
        try
        {
            lock (_cacheLock)
            {
                var cacheKey = $"{modId}_{gameInstall.Id}";
                if (_compatibilityCache.TryGetValue(cacheKey, out var cached))
                {
                    return cached;
                }
            }

            var compatibility = await PerformCompatibilityCheck(modId, gameInstall);
            
            lock (_cacheLock)
            {
                var cacheKey = $"{modId}_{gameInstall.Id}";
                _compatibilityCache[cacheKey] = compatibility;
            }

            return compatibility;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking compatibility: {ex.Message}");
            return new ModCompatibility
            {
                ModId = modId,
                GameVersion = GetGameVersion(gameInstall),
                BepInExVersion = gameInstall.BepInExVersion,
                Status = CompatibilityStatus.Unknown,
                Notes = $"Error during compatibility check: {ex.Message}"
            };
        }
    }

    public async Task<List<ModCompatibility>> CheckCompatibility(List<string> modIds, GameInstallation gameInstall)
    {
        var compatibilities = new List<ModCompatibility>();

        try
        {
            var tasks = modIds.Select(modId => CheckCompatibility(modId, gameInstall));
            var results = await Task.WhenAll(tasks);
            compatibilities.AddRange(results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking compatibility for multiple mods: {ex.Message}");
        }

        return compatibilities;
    }

    public async Task<List<string>> GetRecommendedVersions(string modId, string gameVersion)
    {
        var recommendedVersions = new List<string>();

        try
        {
            var modEntry = await _modIndexService.GetMod(modId);
            if (modEntry == null)
                return recommendedVersions;

            // filter versions by game compatibility
            var compatibleVersions = modEntry.Versions
                .Where(v => IsVersionCompatibleWithGame(v, gameVersion))
                .OrderByDescending(v => ParseVersion(v.Version))
                .ToList();

            // get stable versions first, then beta, then alpha
            var stableVersions = compatibleVersions.Where(v => !v.IsPrerelease).ToList();
            var betaVersions = compatibleVersions.Where(v => v.IsPrerelease && IsBetaVersion(v.Version)).ToList();
            var alphaVersions = compatibleVersions.Where(v => v.IsPrerelease && IsAlphaVersion(v.Version)).ToList();

            recommendedVersions.AddRange(stableVersions.Take(3).Select(v => v.Version));
            recommendedVersions.AddRange(betaVersions.Take(2).Select(v => v.Version));
            recommendedVersions.AddRange(alphaVersions.Take(1).Select(v => v.Version));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting recommended versions: {ex.Message}");
        }

        return recommendedVersions;
    }

    public async Task<OperationResult> ReportCompatibilityIssue(ModCompatibility compatibility)
    {
        try
        {
            // save compatibility report to local file
            var reportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lacesong", "compatibility_reports");
            Directory.CreateDirectory(reportsDir);

            var reportFile = Path.Combine(reportsDir, $"{compatibility.ModId}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            var reportJson = JsonSerializer.Serialize(compatibility, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(reportFile, reportJson);

            // in a full implementation, this would also send the report to a central server
            // for now, we just log it locally

            return OperationResult.SuccessResult("Compatibility issue reported successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to report compatibility issue");
        }
    }

    private async Task<ModCompatibility> PerformCompatibilityCheck(string modId, GameInstallation gameInstall)
    {
        var gameVersion = GetGameVersion(gameInstall);
        var bepinexVersion = gameInstall.BepInExVersion;

        try
        {
            // get mod information
            var modEntry = await _modIndexService.GetMod(modId);
            var installedMod = await _modManager.GetModInfo(modId, gameInstall);

            if (modEntry == null && installedMod == null)
            {
                return new ModCompatibility
                {
                    ModId = modId,
                    GameVersion = gameVersion,
                    BepInExVersion = bepinexVersion,
                    Status = CompatibilityStatus.Unknown,
                    Notes = "Mod not found in index or installation"
                };
            }

            var compatibility = new ModCompatibility
            {
                ModId = modId,
                GameVersion = gameVersion,
                BepInExVersion = bepinexVersion,
                LastTested = DateTime.UtcNow,
                TestedBy = "Lacesong Compatibility Service"
            };

            // check game version compatibility
            if (modEntry != null)
            {
                var gameCompatibilityResult = await CheckGameVersionCompatibility(modEntry, gameVersion);
                compatibility.Status = gameCompatibilityResult.Status;
                compatibility.Issues.AddRange(gameCompatibilityResult.Issues);
                compatibility.Notes = gameCompatibilityResult.Notes;
                compatibility.RecommendedVersions = await GetRecommendedVersions(modId, gameVersion);
            }

            // check bepinex compatibility
            if (modEntry != null)
            {
                var bepinexCompatibilityResult = await CheckBepInExCompatibility(modEntry, bepinexVersion);
                if (bepinexCompatibilityResult.Status < compatibility.Status)
                {
                    compatibility.Status = bepinexCompatibilityResult.Status;
                }
                compatibility.Issues.AddRange(bepinexCompatibilityResult.Issues);
            }

            // check dependency compatibility
            if (installedMod != null)
            {
                var dependencyCompatibilityResult = await CheckDependencyCompatibility(installedMod, gameInstall);
                if (dependencyCompatibilityResult.Status < compatibility.Status)
                {
                    compatibility.Status = dependencyCompatibilityResult.Status;
                }
                compatibility.Issues.AddRange(dependencyCompatibilityResult.Issues);
            }

            // check for known issues
            var knownIssues = await GetKnownIssues(modId, gameVersion);
            compatibility.Issues.AddRange(knownIssues);

            return compatibility;
        }
        catch (Exception ex)
        {
            return new ModCompatibility
            {
                ModId = modId,
                GameVersion = gameVersion,
                BepInExVersion = bepinexVersion,
                Status = CompatibilityStatus.Unknown,
                Notes = $"Error during compatibility check: {ex.Message}"
            };
        }
    }

    private Task<CompatibilityCheckResult> CheckGameVersionCompatibility(ModIndexEntry modEntry, string gameVersion)
    {
        var result = new CompatibilityCheckResult
        {
            Status = CompatibilityStatus.Compatible,
            Notes = "Game version compatibility check passed"
        };

        try
        {
            // check if mod has specific game version requirements
            var compatibleVersions = modEntry.Versions
                .Where(v => IsVersionCompatibleWithGame(v, gameVersion))
                .ToList();

            if (compatibleVersions.Count == 0)
            {
                result.Status = CompatibilityStatus.Incompatible;
                result.Issues.Add($"No versions compatible with game version {gameVersion}");
                result.Notes = "Mod requires a different game version";
            }
            else
            {
                // check if any versions are marked as problematic
                var problematicVersions = compatibleVersions
                    .Where(v => !string.IsNullOrEmpty(v.Changelog) && 
                               (v.Changelog.Contains("incompatible", StringComparison.OrdinalIgnoreCase) ||
                                v.Changelog.Contains("broken", StringComparison.OrdinalIgnoreCase) ||
                                v.Changelog.Contains("not working", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (problematicVersions.Any())
                {
                    result.Status = CompatibilityStatus.CompatibleWithIssues;
                    result.Issues.Add("Some versions may have compatibility issues");
                    result.Notes = "Mod may have issues with current game version";
                }
            }
        }
        catch (Exception ex)
        {
            result.Status = CompatibilityStatus.Unknown;
            result.Issues.Add($"Error checking game compatibility: {ex.Message}");
            result.Notes = "Unable to determine game version compatibility";
        }

        return Task.FromResult(result);
    }

    private Task<CompatibilityCheckResult> CheckBepInExCompatibility(ModIndexEntry modEntry, string bepinexVersion)
    {
        var result = new CompatibilityCheckResult
        {
            Status = CompatibilityStatus.Compatible,
            Notes = "BepInEx version compatibility check passed"
        };

        try
        {
            var currentBepInExVersion = ParseVersion(bepinexVersion);
            
            foreach (var version in modEntry.Versions)
            {
                if (!string.IsNullOrEmpty(version.BepInExVersion))
                {
                    var requiredVersion = ParseVersion(version.BepInExVersion);
                    
                    if (currentBepInExVersion < requiredVersion)
                    {
                        result.Status = CompatibilityStatus.Incompatible;
                        result.Issues.Add($"BepInEx version {bepinexVersion} is older than required {version.BepInExVersion}");
                        result.Notes = "BepInEx version too old";
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Status = CompatibilityStatus.Unknown;
            result.Issues.Add($"Error checking BepInEx compatibility: {ex.Message}");
            result.Notes = "Unable to determine BepInEx compatibility";
        }

        return Task.FromResult(result);
    }

    private async Task<CompatibilityCheckResult> CheckDependencyCompatibility(ModInfo mod, GameInstallation gameInstall)
    {
        var result = new CompatibilityCheckResult
        {
            Status = CompatibilityStatus.Compatible,
            Notes = "Dependency compatibility check passed"
        };

        try
        {
            if (mod.Dependencies.Count == 0)
                return result;

            var installedMods = await _modManager.GetInstalledMods(gameInstall);
            var missingDependencies = new List<string>();

            foreach (var dependency in mod.Dependencies)
            {
                var installedDependency = installedMods.FirstOrDefault(m => 
                    m.Id.Equals(dependency, StringComparison.OrdinalIgnoreCase));
                
                if (installedDependency == null)
                {
                    missingDependencies.Add(dependency);
                }
            }

            if (missingDependencies.Any())
            {
                result.Status = CompatibilityStatus.Incompatible;
                result.Issues.Add($"Missing dependencies: {string.Join(", ", missingDependencies)}");
                result.Notes = "Required dependencies not installed";
            }
        }
        catch (Exception ex)
        {
            result.Status = CompatibilityStatus.Unknown;
            result.Issues.Add($"Error checking dependency compatibility: {ex.Message}");
            result.Notes = "Unable to check dependency compatibility";
        }

        return result;
    }

    private async Task<List<string>> GetKnownIssues(string modId, string gameVersion)
    {
        var issues = new List<string>();

        try
        {
            // load known issues from local database or file
            var issuesFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Lacesong", "known_issues.json");
            
            if (File.Exists(issuesFile))
            {
                var json = await File.ReadAllTextAsync(issuesFile);
                var knownIssues = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json) ?? new Dictionary<string, List<string>>();
                
                var modIssues = knownIssues.GetValueOrDefault(modId, new List<string>());
                var versionIssues = knownIssues.GetValueOrDefault($"{modId}_{gameVersion}", new List<string>());
                
                issues.AddRange(modIssues);
                issues.AddRange(versionIssues);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading known issues: {ex.Message}");
        }

        return issues;
    }

    private bool IsVersionCompatibleWithGame(ModVersion version, string gameVersion)
    {
        if (string.IsNullOrEmpty(version.GameVersion))
            return true; // no specific requirement

        return version.GameVersion == gameVersion || 
               IsVersionInRange(gameVersion, version.GameVersion);
    }

    private bool IsVersionInRange(string currentVersion, string requiredVersion)
    {
        try
        {
            // simple version range checking
            // this could be enhanced to support complex version ranges
            var current = ParseVersion(currentVersion);
            var required = ParseVersion(requiredVersion);
            
            return current >= required;
        }
        catch
        {
            return false;
        }
    }

    private bool IsBetaVersion(string version)
    {
        return version.ToLowerInvariant().Contains("beta") || 
               version.ToLowerInvariant().Contains("b");
    }

    private bool IsAlphaVersion(string version)
    {
        return version.ToLowerInvariant().Contains("alpha") || 
               version.ToLowerInvariant().Contains("a") ||
               version.ToLowerInvariant().Contains("preview") ||
               version.ToLowerInvariant().Contains("dev");
    }

    private string GetGameVersion(GameInstallation gameInstall)
    {
        // try to read game version from various sources
        var versionSources = new[]
        {
            Path.Combine(gameInstall.InstallPath, "version.txt"),
            Path.Combine(gameInstall.InstallPath, "game_version.txt"),
            Path.Combine(gameInstall.InstallPath, "silksong_version.txt")
        };

        foreach (var source in versionSources)
        {
            if (File.Exists(source))
            {
                try
                {
                    return File.ReadAllText(source).Trim();
                }
                catch
                {
                    continue;
                }
            }
        }

        // fallback to detecting from executable
        var executablePath = Path.Combine(gameInstall.InstallPath, gameInstall.Executable);
        if (File.Exists(executablePath))
        {
            try
            {
                var fileInfo = FileVersionInfo.GetVersionInfo(executablePath);
                return fileInfo.FileVersion ?? "1.0.0";
            }
            catch
            {
                // ignore
            }
        }

        return "1.0.0"; // default version
    }

    private Version ParseVersion(string versionString)
    {
        try
        {
            // clean version string
            var cleanVersion = Regex.Replace(versionString, @"[^\d\.\-]", "");
            
            // handle pre-release versions
            if (cleanVersion.Contains('-'))
            {
                cleanVersion = cleanVersion.Split('-')[0];
            }

            return Version.Parse(cleanVersion);
        }
        catch
        {
            return new Version(0, 0, 0);
        }
    }

    private class CompatibilityCheckResult
    {
        public CompatibilityStatus Status { get; set; } = CompatibilityStatus.Unknown;
        public List<string> Issues { get; set; } = new();
        public string Notes { get; set; } = string.Empty;
    }
}

using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using System.Text.RegularExpressions;

namespace Lacesong.Core.Services;

/// <summary>
/// service for resolving mod dependencies and checking compatibility
/// </summary>
public class DependencyResolver : IDependencyResolver
{
    private readonly IModManager _modManager;
    private readonly IBepInExManager _bepinexManager;

    public DependencyResolver(IModManager modManager, IBepInExManager bepinexManager)
    {
        _modManager = modManager;
        _bepinexManager = bepinexManager;
    }

    public async Task<DependencyResolution> ResolveDependencies(ModInfo modInfo, GameInstallation gameInstall)
    {
        var resolution = new DependencyResolution();

        try
        {
            // check bepinex compatibility
            var bepinexResult = await CheckBepInExCompatibility(modInfo, gameInstall);
            resolution.BepInExVersion = bepinexResult.BepInExVersion;
            resolution.BepInExCompatible = bepinexResult.IsCompatible;

            if (!bepinexResult.IsCompatible)
            {
                resolution.Conflicts.Add(new DependencyConflict
                {
                    ModId = modInfo.Id,
                    ConflictType = "BepInExIncompatible",
                    Description = $"Mod requires BepInEx {bepinexResult.RequiredVersion}, but {bepinexResult.InstalledVersion ?? "none"} is installed"
                });
            }

            // resolve mod dependencies
            var installedMods = await _modManager.GetInstalledMods(gameInstall);
            var modDependencies = new List<ModDependency>();

            // parse dependencies from mod info
            foreach (var dependencyId in modInfo.Dependencies)
            {
                var dependency = ParseDependencyString(dependencyId);
                modDependencies.Add(dependency);
            }

            foreach (var dependency in modDependencies)
            {
                var installedMod = installedMods.FirstOrDefault(m => m.Id.Equals(dependency.Id, StringComparison.OrdinalIgnoreCase));
                
                if (installedMod == null)
                {
                    if (dependency.IsOptional)
                    {
                        // optional dependency not found - this is ok
                        continue;
                    }
                    else
                    {
                        resolution.Missing.Add(dependency);
                    }
                }
                else
                {
                    // check version compatibility
                    if (!string.IsNullOrEmpty(dependency.Version))
                    {
                        var isCompatible = IsVersionCompatible(installedMod.Version, dependency.Version);
                        if (!isCompatible)
                        {
                            resolution.Conflicts.Add(new DependencyConflict
                            {
                                ModId = dependency.Id,
                                RequiredVersion = dependency.Version,
                                InstalledVersion = installedMod.Version,
                                ConflictType = "VersionMismatch",
                                Description = $"Required version {dependency.Version} but installed version {installedMod.Version}"
                            });
                        }
                    }

                    resolution.Resolved.Add(dependency);
                }
            }

            // check for circular dependencies
            var circularDeps = CheckCircularDependencies(modInfo, installedMods);
            resolution.Conflicts.AddRange(circularDeps);

            // determine if resolution is valid
            resolution.IsValid = resolution.BepInExCompatible && 
                                resolution.Missing.Count == 0 && 
                                resolution.Conflicts.Count == 0;

            return resolution;
        }
        catch (Exception ex)
        {
            return new DependencyResolution
            {
                IsValid = false,
                Conflicts = new List<DependencyConflict>
                {
                    new DependencyConflict
                    {
                        ModId = modInfo.Id,
                        ConflictType = "ResolutionError",
                        Description = $"Dependency resolution failed: {ex.Message}"
                    }
                }
            };
        }
    }

    public async Task<OperationResult> InstallMissingDependencies(DependencyResolution resolution, GameInstallation gameInstall)
    {
        try
        {
            var results = new List<OperationResult>();

            foreach (var missingDependency in resolution.Missing)
            {
                // try to find the dependency in available mod repositories
                var installResult = await InstallDependency(missingDependency, gameInstall);
                results.Add(installResult);

                if (!installResult.Success)
                {
                    return OperationResult.ErrorResult(
                        $"Failed to install dependency {missingDependency.Id}: {installResult.Error}",
                        "Dependency installation failed"
                    );
                }
            }

            return OperationResult.SuccessResult("All missing dependencies installed successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Dependency installation failed");
        }
    }

    private async Task<(string? BepInExVersion, string? RequiredVersion, string? InstalledVersion, bool IsCompatible)> CheckBepInExCompatibility(ModInfo modInfo, GameInstallation gameInstall)
    {
        try
        {
            var installedVersion = _bepinexManager.GetInstalledBepInExVersion(gameInstall);
            var requiredVersion = ExtractBepInExRequirement(modInfo);

            if (string.IsNullOrEmpty(requiredVersion))
            {
                // no specific requirement - assume compatible with any version
                return (installedVersion, null, installedVersion, true);
            }

            if (string.IsNullOrEmpty(installedVersion))
            {
                // bepinex not installed
                return (null, requiredVersion, null, false);
            }

            var isCompatible = IsVersionCompatible(installedVersion, requiredVersion);
            return (installedVersion, requiredVersion, installedVersion, isCompatible);
        }
        catch
        {
            return (null, null, null, false);
        }
    }

    private string? ExtractBepInExRequirement(ModInfo modInfo)
    {
        // check if mod has bepinex version requirement in description or dependencies
        // this is a simplified implementation - in practice, this might be in mod metadata
        
        // look for common patterns in description
        var bepinexPattern = @"BepInEx\s+([0-9]+\.[0-9]+(?:\.[0-9]+)?)";
        var match = Regex.Match(modInfo.Description, bepinexPattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // check dependencies for bepinex requirement
        foreach (var dependency in modInfo.Dependencies)
        {
            if (dependency.ToLower().Contains("bepinex"))
            {
                var versionMatch = Regex.Match(dependency, @"([0-9]+\.[0-9]+(?:\.[0-9]+)?)");
                if (versionMatch.Success)
                {
                    return versionMatch.Groups[1].Value;
                }
            }
        }

        return null;
    }

    private ModDependency ParseDependencyString(string dependencyString)
    {
        var dependency = new ModDependency { Id = dependencyString };

        // parse version constraints (e.g., "mod-id>=1.0.0", "mod-id~1.0.0")
        var versionMatch = Regex.Match(dependencyString, @"^(.+?)([>=<~]+)(.+)$");
        if (versionMatch.Success)
        {
            dependency.Id = versionMatch.Groups[1].Value.Trim();
            var constraint = versionMatch.Groups[2].Value;
            var version = versionMatch.Groups[3].Value.Trim();

            // normalize version constraint
            if (constraint.Contains(">="))
            {
                dependency.Version = $">={version}";
            }
            else if (constraint.Contains("<="))
            {
                dependency.Version = $"<={version}";
            }
            else if (constraint.Contains("~"))
            {
                dependency.Version = $"~{version}";
            }
            else
            {
                dependency.Version = version;
            }
        }

        return dependency;
    }

    private bool IsVersionCompatible(string installedVersion, string requiredVersion)
    {
        try
        {
            // normalize versions
            var installed = NormalizeVersion(installedVersion);
            var required = requiredVersion.Trim();

            if (string.IsNullOrEmpty(required))
            {
                return true;
            }

            // handle version constraints
            if (required.StartsWith(">="))
            {
                var targetVersion = NormalizeVersion(required.Substring(2));
                return CompareVersions(installed, targetVersion) >= 0;
            }
            else if (required.StartsWith("<="))
            {
                var targetVersion = NormalizeVersion(required.Substring(2));
                return CompareVersions(installed, targetVersion) <= 0;
            }
            else if (required.StartsWith("~"))
            {
                // tilde range - compatible within same minor version
                var targetVersion = NormalizeVersion(required.Substring(1));
                return IsCompatibleTildeRange(installed, targetVersion);
            }
            else
            {
                // exact version match
                var targetVersion = NormalizeVersion(required);
                return CompareVersions(installed, targetVersion) == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    private Version NormalizeVersion(string version)
    {
        // remove any non-numeric characters except dots
        var cleanVersion = Regex.Replace(version, @"[^\d\.]", "");
        
        // ensure we have at least major.minor
        var parts = cleanVersion.Split('.');
        if (parts.Length < 2)
        {
            cleanVersion += ".0";
        }
        if (parts.Length < 3)
        {
            cleanVersion += ".0";
        }

        return Version.Parse(cleanVersion);
    }

    private int CompareVersions(Version v1, Version v2)
    {
        return v1.CompareTo(v2);
    }

    private bool IsCompatibleTildeRange(Version installed, Version target)
    {
        // tilde range: ~1.2.3 is compatible with >=1.2.3 and <1.3.0
        return installed >= target && installed < new Version(target.Major, target.Minor + 1, 0);
    }

    private List<DependencyConflict> CheckCircularDependencies(ModInfo modInfo, List<ModInfo> installedMods)
    {
        var conflicts = new List<DependencyConflict>();
        
        // simple circular dependency check
        // in a real implementation, this would be more sophisticated
        foreach (var dependency in modInfo.Dependencies)
        {
            var installedMod = installedMods.FirstOrDefault(m => m.Id.Equals(dependency, StringComparison.OrdinalIgnoreCase));
            if (installedMod != null && installedMod.Dependencies.Contains(modInfo.Id))
            {
                conflicts.Add(new DependencyConflict
                {
                    ModId = dependency,
                    ConflictType = "CircularDependency",
                    Description = $"Circular dependency detected between {modInfo.Id} and {dependency}"
                });
            }
        }

        return conflicts;
    }

    private async Task<OperationResult> InstallDependency(ModDependency dependency, GameInstallation gameInstall)
    {
        try
        {
            // this is a simplified implementation
            // in practice, this would search mod repositories for the dependency
            return OperationResult.ErrorResult(
                $"Dependency {dependency.Id} not found in available repositories",
                "Dependency not available"
            );
        }
        catch (Exception ex)
        {
            return OperationResult.ErrorResult(ex.Message, "Failed to install dependency");
        }
    }
}

#!/usr/bin/env dotnet-script
#r "nuget: System.Text.Json, 8.0.0"
#r "nuget: Octokit, 3.0.0"

using System.Text.Json;
using Octokit;
using Lacesong.Core.Models;

/// <summary>
/// admin script for curating and publishing the lacesong mod index
/// </summary>
class ModIndexAdmin
{
    private readonly GitHubClient _githubClient;
    private readonly string _outputPath;
    private readonly List<string> _curatedRepositories;

    public ModIndexAdmin(string? githubToken = null)
    {
        _githubClient = new GitHubClient(new ProductHeaderValue("Lacesong-Admin"));
        if (!string.IsNullOrEmpty(githubToken))
        {
            _githubClient.Credentials = new Credentials(githubToken);
        }

        _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "mods.json");
        
        // curated list of repositories to scan for mods
        _curatedRepositories = new List<string>
        {
            "TeamCherry/HollowKnight",
            "BepInEx/BepInEx",
            "hollow-knight-community/HollowKnightModding",
            // add more curated repositories here
        };
    }

    public async Task<int> RunAsync(string[] args)
    {
        try
        {
            var command = args.Length > 0 ? args[0] : "build";
            
            switch (command.ToLowerInvariant())
            {
                case "build":
                    await BuildIndex();
                    break;
                case "validate":
                    await ValidateIndex();
                    break;
                case "publish":
                    await PublishIndex();
                    break;
                case "scan-repo":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Usage: scan-repo <owner> <repo>");
                        return 1;
                    }
                    await ScanRepository(args[1], args[2]);
                    break;
                case "add-repo":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Usage: add-repo <owner> <repo>");
                        return 1;
                    }
                    await AddRepository(args[1], args[2]);
                    break;
                default:
                    ShowHelp();
                    return 1;
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private async Task BuildIndex()
    {
        Console.WriteLine("Building mod index...");
        
        var modIndex = new ModIndex
        {
            Version = "1.0.0",
            LastUpdated = DateTime.UtcNow,
            Categories = new List<string>
            {
                "General", "UI", "Gameplay", "Graphics", "Audio", "Utility", "Developer"
            },
            Mods = new List<ModIndexEntry>(),
            Repositories = new List<ModRepository>()
        };

        // scan curated repositories
        foreach (var repo in _curatedRepositories)
        {
            var parts = repo.Split('/');
            if (parts.Length == 2)
            {
                Console.WriteLine($"Scanning repository: {repo}");
                var mods = await ScanRepositoryForMods(parts[0], parts[1]);
                modIndex.Mods.AddRange(mods);
            }
        }

        // add repository configurations
        foreach (var repo in _curatedRepositories)
        {
            var parts = repo.Split('/');
            if (parts.Length == 2)
            {
                modIndex.Repositories.Add(new ModRepository
                {
                    Id = repo.Replace("/", "-").ToLowerInvariant(),
                    Name = $"{parts[0]}/{parts[1]}",
                    Url = $"https://github.com/{repo}",
                    Type = "GitHub",
                    IsOfficial = true,
                    IsEnabled = true,
                    Description = $"Curated repository: {repo}"
                });
            }
        }

        modIndex.TotalMods = modIndex.Mods.Count;

        // save index
        var json = JsonSerializer.Serialize(modIndex, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_outputPath, json);
        
        Console.WriteLine($"Mod index built successfully with {modIndex.Mods.Count} mods from {modIndex.Repositories.Count} repositories");
        Console.WriteLine($"Index saved to: {_outputPath}");
    }

    private async Task ValidateIndex()
    {
        Console.WriteLine("Validating mod index...");
        
        if (!File.Exists(_outputPath))
        {
            Console.WriteLine("Error: mods.json not found. Run 'build' first.");
            return;
        }

        var json = await File.ReadAllTextAsync(_outputPath);
        var modIndex = JsonSerializer.Deserialize<ModIndex>(json);
        
        if (modIndex == null)
        {
            Console.WriteLine("Error: Failed to parse mod index");
            return;
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        // validate structure
        if (modIndex.Mods.Count == 0)
        {
            errors.Add("No mods found in index");
        }

        if (modIndex.Repositories.Count == 0)
        {
            errors.Add("No repositories configured");
        }

        // validate mods
        foreach (var mod in modIndex.Mods)
        {
            if (string.IsNullOrEmpty(mod.Id))
                errors.Add($"Mod missing ID");
            
            if (string.IsNullOrEmpty(mod.Name))
                errors.Add($"Mod '{mod.Id}' missing name");
            
            if (string.IsNullOrEmpty(mod.Author))
                warnings.Add($"Mod '{mod.Id}' missing author");
            
            if (mod.Versions.Count == 0)
                errors.Add($"Mod '{mod.Id}' has no versions");
            
            foreach (var version in mod.Versions)
            {
                if (string.IsNullOrEmpty(version.DownloadUrl))
                    errors.Add($"Mod '{mod.Id}' version '{version.Version}' missing download URL");
                
                if (version.FileSize <= 0)
                    warnings.Add($"Mod '{mod.Id}' version '{version.Version}' has invalid file size");
            }
        }

        // validate repositories
        foreach (var repo in modIndex.Repositories)
        {
            if (string.IsNullOrEmpty(repo.Url))
                errors.Add($"Repository '{repo.Id}' missing URL");
            
            if (!Uri.IsWellFormedUriString(repo.Url, UriKind.Absolute))
                errors.Add($"Repository '{repo.Id}' has invalid URL");
        }

        // report results
        if (errors.Count > 0)
        {
            Console.WriteLine("Validation Errors:");
            foreach (var error in errors)
            {
                Console.WriteLine($"  ❌ {error}");
            }
        }

        if (warnings.Count > 0)
        {
            Console.WriteLine("Validation Warnings:");
            foreach (var warning in warnings)
            {
                Console.WriteLine($"  ⚠️  {warning}");
            }
        }

        if (errors.Count == 0 && warnings.Count == 0)
        {
            Console.WriteLine("✅ Mod index validation passed");
        }
        else if (errors.Count == 0)
        {
            Console.WriteLine("✅ Mod index validation passed with warnings");
        }
        else
        {
            Console.WriteLine($"❌ Mod index validation failed with {errors.Count} errors");
        }
    }

    private async Task PublishIndex()
    {
        Console.WriteLine("Publishing mod index...");
        
        if (!File.Exists(_outputPath))
        {
            Console.WriteLine("Error: mods.json not found. Run 'build' first.");
            return;
        }

        // validate before publishing
        await ValidateIndex();
        
        // here you would typically upload to your hosting service
        // for now, just copy to a web-accessible location
        var publishPath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "mods.json");
        Directory.CreateDirectory(Path.GetDirectoryName(publishPath)!);
        
        File.Copy(_outputPath, publishPath, true);
        Console.WriteLine($"Mod index published to: {publishPath}");
        
        // in a real implementation, you might:
        // - Upload to S3
        // - Deploy to GitHub Pages
        // - Push to CDN
        // - Update database
    }

    private async Task ScanRepository(string owner, string repo)
    {
        Console.WriteLine($"Scanning repository: {owner}/{repo}");
        
        try
        {
            var releases = await _githubClient.Repository.Release.GetAll(owner, repo);
            Console.WriteLine($"Found {releases.Count} releases");
            
            foreach (var release in releases.Take(5)) // show last 5 releases
            {
                Console.WriteLine($"  Release: {release.TagName} - {release.Name}");
                Console.WriteLine($"    Published: {release.PublishedAt}");
                Console.WriteLine($"    Prerelease: {release.Prerelease}");
                Console.WriteLine($"    Assets: {release.Assets.Count}");
                
                foreach (var asset in release.Assets)
                {
                    Console.WriteLine($"      Asset: {asset.Name} ({asset.Size} bytes)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning repository: {ex.Message}");
        }
    }

    private async Task AddRepository(string owner, string repo)
    {
        Console.WriteLine($"Adding repository: {owner}/{repo}");
        
        try
        {
            var repository = await _githubClient.Repository.Get(owner, repo);
            Console.WriteLine($"Repository found: {repository.Name}");
            Console.WriteLine($"Description: {repository.Description}");
            Console.WriteLine($"Stars: {repository.StargazersCount}");
            Console.WriteLine($"Language: {repository.Language}");
            
            // check if it has releases
            var releases = await _githubClient.Repository.Release.GetAll(owner, repo);
            Console.WriteLine($"Releases: {releases.Count}");
            
            if (releases.Count > 0)
            {
                Console.WriteLine("This repository has releases and can be added to the curated list.");
                Console.WriteLine("Add it to the _curatedRepositories list in the script.");
            }
            else
            {
                Console.WriteLine("This repository has no releases.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding repository: {ex.Message}");
        }
    }

    private async Task<List<ModIndexEntry>> ScanRepositoryForMods(string owner, string repo)
    {
        var mods = new List<ModIndexEntry>();
        
        try
        {
            var releases = await _githubClient.Repository.Release.GetAll(owner, repo);
            
            foreach (var release in releases.Where(r => !r.Prerelease))
            {
                if (release.Assets.Count > 0)
                {
                    var modEntry = new ModIndexEntry
                    {
                        Id = $"{owner}-{repo}".ToLowerInvariant(),
                        Name = repo,
                        Description = repository.Description ?? "No description available",
                        Author = owner,
                        Repository = $"https://github.com/{owner}/{repo}",
                        Category = "General",
                        Tags = new List<string> { "github", "community" },
                        GameCompatibility = new List<string> { "hollow-knight-silksong" },
                        IsOfficial = false,
                        IsVerified = false,
                        LastUpdated = release.PublishedAt ?? DateTime.UtcNow,
                        DownloadCount = release.Assets.Sum(a => a.DownloadCount),
                        Versions = new List<ModVersion>
                        {
                            new ModVersion
                            {
                                Version = release.TagName.TrimStart('v'),
                                DownloadUrl = release.Assets.FirstOrDefault()?.BrowserDownloadUrl ?? "",
                                FileName = release.Assets.FirstOrDefault()?.Name ?? "",
                                FileSize = release.Assets.FirstOrDefault()?.Size ?? 0,
                                ReleaseDate = release.PublishedAt ?? DateTime.UtcNow,
                                Changelog = release.Body,
                                IsPrerelease = release.Prerelease,
                                Dependencies = new List<ModDependency>(),
                                Conflicts = new List<string>()
                            }
                        }
                    };
                    
                    mods.Add(modEntry);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning repository {owner}/{repo}: {ex.Message}");
        }
        
        return mods;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Lacesong Mod Index Admin Tool");
        Console.WriteLine();
        Console.WriteLine("Usage: dotnet script admin.csx <command> [args]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  build              Build the mod index from curated repositories");
        Console.WriteLine("  validate           Validate the mod index for errors and warnings");
        Console.WriteLine("  publish            Publish the mod index to hosting location");
        Console.WriteLine("  scan-repo <owner> <repo>  Scan a specific repository for mods");
        Console.WriteLine("  add-repo <owner> <repo>   Add a repository to the curated list");
        Console.WriteLine();
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  GITHUB_TOKEN       GitHub API token for higher rate limits");
    }
}

// main execution
var admin = new ModIndexAdmin(Environment.GetEnvironmentVariable("GITHUB_TOKEN"));
var exitCode = await admin.RunAsync(args);
Environment.Exit(exitCode);

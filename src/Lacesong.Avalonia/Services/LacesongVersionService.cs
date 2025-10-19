using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.Services;

/// <summary>
/// service for checking lacesong version updates from github
/// </summary>
public class LacesongVersionService : ILacesongVersionService
{
    private readonly ILogger<LacesongVersionService> _logger;
    private readonly GitHubClient _githubClient;
    private const string RepositoryOwner = "pecan-solutions";
    private const string RepositoryName = "lacesong";

    public LacesongVersionService(ILogger<LacesongVersionService> logger)
    {
        _logger = logger;
        _githubClient = new GitHubClient(new ProductHeaderValue("Lacesong"));
    }

    public async Task<LacesongVersionInfo> CheckForUpdatesAsync()
    {
        try
        {
            _logger.LogInformation("Checking for Lacesong updates...");
            
            var releases = await _githubClient.Repository.Release.GetAll(RepositoryOwner, RepositoryName);
            var latestRelease = releases.FirstOrDefault(r => !r.Prerelease);
            
            if (latestRelease == null)
            {
                _logger.LogWarning("No releases found");
                return new LacesongVersionInfo { IsUpdateAvailable = false };
            }

            var currentVersion = GetCurrentVersion();
            var latestVersion = latestRelease.TagName.TrimStart('v');
            
            var isUpdateAvailable = IsNewerVersion(latestVersion, currentVersion);

            var versionInfo = new LacesongVersionInfo
            {
                IsUpdateAvailable = isUpdateAvailable,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                DownloadUrl = latestRelease.Assets.FirstOrDefault()?.BrowserDownloadUrl ?? string.Empty,
                ReleaseNotes = latestRelease.Body ?? string.Empty,
                ReleaseDate = latestRelease.PublishedAt?.DateTime ?? DateTime.MinValue
            };

            if (isUpdateAvailable)
            {
                _logger.LogInformation($"Lacesong update available: {latestVersion} (current: {currentVersion})");
            }
            else
            {
                _logger.LogInformation($"Lacesong is up to date (current: {currentVersion}, latest: {latestVersion})");
            }

            return versionInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for Lacesong updates");
            return new LacesongVersionInfo { IsUpdateAvailable = false };
        }
    }

    private string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return $"{version?.Major}.{version?.Minor}.{version?.Build}";
    }

    private bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        try
        {
            var latest = Version.Parse(latestVersion);
            var current = Version.Parse(currentVersion);
            return latest > current;
        }
        catch
        {
            return false;
        }
    }
}

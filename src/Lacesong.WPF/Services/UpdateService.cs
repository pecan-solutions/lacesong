using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Lacesong.WPF.Services;

/// <summary>
/// implementation of update service using github releases
/// </summary>
public class UpdateService : IUpdateService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly GitHubClient _githubClient;
    private readonly string _repositoryOwner;
    private readonly string _repositoryName;

    public bool IsUpdateAvailable { get; private set; }
    public string LatestVersion { get; private set; } = string.Empty;

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
        _githubClient = new GitHubClient(new ProductHeaderValue("Lacesong"));
        // temporarily disable update checking by using a non-existent repo
        // this prevents startup crashes during development
        _repositoryOwner = "placeholder"; 
        _repositoryName = "placeholder"; 
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync()
    {
        try
        {
            _logger.LogInformation("Checking for updates...");
            
            // skip update check during development with placeholder repo
            if (_repositoryOwner == "placeholder" || _repositoryName == "placeholder")
            {
                _logger.LogInformation("Update checking disabled for development");
                return new UpdateInfo { IsUpdateAvailable = false };
            }
            
            var releases = await _githubClient.Repository.Release.GetAll(_repositoryOwner, _repositoryName);
            var latestRelease = releases.FirstOrDefault(r => !r.Prerelease);
            
            if (latestRelease == null)
            {
                _logger.LogWarning("No releases found");
                return new UpdateInfo { IsUpdateAvailable = false };
            }

            var currentVersion = GetCurrentVersion();
            var latestVersion = latestRelease.TagName.TrimStart('v');
            
            LatestVersion = latestVersion;
            IsUpdateAvailable = IsNewerVersion(latestVersion, currentVersion);

            var updateInfo = new UpdateInfo
            {
                IsUpdateAvailable = IsUpdateAvailable,
                LatestVersion = latestVersion,
                DownloadUrl = latestRelease.Assets.FirstOrDefault()?.BrowserDownloadUrl ?? string.Empty,
                ReleaseNotes = latestRelease.Body ?? string.Empty,
                ReleaseDate = latestRelease.PublishedAt?.DateTime ?? DateTime.MinValue
            };

            if (IsUpdateAvailable)
            {
                _logger.LogInformation($"Update available: {latestVersion} (current: {currentVersion})");
            }
            else
            {
                _logger.LogInformation($"No update available (current: {currentVersion}, latest: {latestVersion})");
            }

            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            return new UpdateInfo { IsUpdateAvailable = false };
        }
    }

    public async Task DownloadUpdateAsync()
    {
        try
        {
            _logger.LogInformation("Downloading update...");
            
            var releases = await _githubClient.Repository.Release.GetAll(_repositoryOwner, _repositoryName);
            var latestRelease = releases.FirstOrDefault(r => !r.Prerelease);
            
            if (latestRelease == null)
            {
                throw new InvalidOperationException("No release found to download");
            }

            var asset = latestRelease.Assets.FirstOrDefault();
            if (asset == null)
            {
                throw new InvalidOperationException("No download asset found");
            }

            // download the update file
            var downloadPath = Path.Combine(Path.GetTempPath(), asset.Name);
            
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(asset.BrowserDownloadUrl);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(downloadPath, content);
            
            _logger.LogInformation($"Update downloaded to: {downloadPath}");
            
            // launch the installer
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download update");
            throw;
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

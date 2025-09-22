using System.Text.Json.Serialization;

namespace Lacesong.Core.Models;

/// <summary>
/// represents a thunderstore package summary including latest versions.
/// </summary>
public class ThunderstorePackage
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("package_url")]
    public string PackageUrl { get; set; } = string.Empty;

    [JsonPropertyName("donation_link")]
    public string? DonationLink { get; set; }

    [JsonPropertyName("date_created")]
    public DateTime DateCreated { get; set; }

    [JsonPropertyName("date_updated")]
    public DateTime DateUpdated { get; set; }

    [JsonPropertyName("uuid4")]
    public string Uuid { get; set; } = string.Empty;

    [JsonPropertyName("rating_score")]
    public int RatingScore { get; set; }

    [JsonPropertyName("is_pinned")]
    public bool IsPinned { get; set; }

    [JsonPropertyName("is_deprecated")]
    public bool IsDeprecated { get; set; }

    [JsonPropertyName("has_nsfw_content")]
    public bool HasNsfwContent { get; set; }

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();

    [JsonPropertyName("versions")]
    public List<ThunderstoreVersion> Versions { get; set; } = new();
}

/// <summary>
/// represents a version entry inside a thunderstore package.
/// </summary>
public class ThunderstoreVersion
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("version_number")]
    public string VersionNumber { get; set; } = string.Empty;

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("downloads")]
    public long Downloads { get; set; }

    [JsonPropertyName("date_created")]
    public DateTime DateCreated { get; set; }

    [JsonPropertyName("website_url")]
    public string? WebsiteUrl { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("uuid4")]
    public string Uuid { get; set; } = string.Empty;

    [JsonPropertyName("file_size")]
    public long FileSize { get; set; }
}

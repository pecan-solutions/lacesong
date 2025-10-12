using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Lacesong.Avalonia.Services;
using Lacesong.Core.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.Models;

public partial class ModDisplayItem : ObservableObject
{
    private readonly ModIndexEntry _modEntry;

    [ObservableProperty]
    private Bitmap? _icon;

    public ModDisplayItem(ModIndexEntry modEntry)
    {
        _modEntry = modEntry;
        _ = LoadIconAsync();
    }

    private async Task LoadIconAsync()
    {
        if (!string.IsNullOrEmpty(_modEntry.Icon))
        {
            Icon = await ImageCacheService.Instance.LoadImageAsync(_modEntry.Icon);
        }
    }

    // expose all properties from ModIndexEntry
    public string Id => _modEntry.Id;
    public string Name => _modEntry.Name;
    public string Description => _modEntry.Description;
    public string Author => _modEntry.Author;
    public string Repository => _modEntry.Repository;
    public string Category => _modEntry.Category;
    public double Rating => _modEntry.Rating;
    public long DownloadCount => _modEntry.DownloadCount;
    public System.DateTime LastUpdated => _modEntry.LastUpdated;
    public System.Collections.Generic.List<ModVersion> Versions => _modEntry.Versions;
    public ModIndexEntry ModEntry => _modEntry;
    
    // safe accessor for latest version that handles empty versions list
    public string LatestVersion => Versions?.FirstOrDefault()?.Version ?? "N/A";
}


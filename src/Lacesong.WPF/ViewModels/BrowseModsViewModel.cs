using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using Lacesong.WPF.Services;

namespace Lacesong.WPF.ViewModels;

/// <summary>
/// view model responsible for browsing and searching mods from thunderstore.
/// </summary>
public partial class BrowseModsViewModel : BaseViewModel
{
    private readonly IThunderstoreApiService _api;
    private readonly ICollectionView _packagesView;
    private readonly IModManager _modManager;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogService _dialogService;
    private GameInstallation? _gameInstall;

    [ObservableProperty]
    private ObservableCollection<ThunderstorePackage> _packages = new();

    [ObservableProperty]
    private ObservableCollection<string> _categories = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private bool _includeNsfw = false;

    [ObservableProperty]
    private bool _includeDeprecated = false;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _sortOption = "Downloads"; // Downloads, Rating, Updated, Name

    public BrowseModsViewModel(ILogger<BrowseModsViewModel> logger,
                               IThunderstoreApiService api,
                               IModManager modManager,
                               ISnackbarService snackbar,
                               IDialogService dialog) : base(logger)
    {
        _api = api;
        _modManager = modManager;
        _snackbar = snackbar;
        _dialogService = dialog;
        Categories.Add("All");
        _packagesView = CollectionViewSource.GetDefaultView(Packages);
        _packagesView.Filter = FilterPackage;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SearchText) or nameof(SelectedCategory) or nameof(IncludeNsfw) or nameof(IncludeDeprecated))
            {
                _packagesView.Refresh();
            }
            if (e.PropertyName == nameof(SortOption))
            {
                ApplySort();
            }
        };
        _ = LoadPackagesAsync();
    }

    public void SetGameInstallation(GameInstallation install)
    {
        _gameInstall = install;
        if (Packages.Count == 0)
        {
            _ = LoadPackagesAsync();
        }
    }

    private bool FilterPackage(object obj)
    {
        if (obj is not ThunderstorePackage p) return false;
        // nsfw/deprecated filters
        if (!IncludeNsfw && p.HasNsfwContent) return false;
        if (!IncludeDeprecated && p.IsDeprecated) return false;
        // category
        if (SelectedCategory != "All" && !p.Categories.Contains(SelectedCategory)) return false;
        // search text across name and description
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim().ToLowerInvariant();
            if (!p.FullName.ToLowerInvariant().Contains(q) &&
                !p.Versions[0].Description.ToLowerInvariant().Contains(q))
                return false;
        }
        return true;
    }

    private void ApplySort()
    {
        using (_packagesView.DeferRefresh())
        {
            _packagesView.SortDescriptions.Clear();
            switch (SortOption)
            {
                case "Downloads":
                    _packagesView.SortDescriptions.Add(new SortDescription("Versions[0].Downloads", ListSortDirection.Descending));
                    break;
                case "Rating":
                    _packagesView.SortDescriptions.Add(new SortDescription("RatingScore", ListSortDirection.Descending));
                    break;
                case "Updated":
                    _packagesView.SortDescriptions.Add(new SortDescription("DateUpdated", ListSortDirection.Descending));
                    break;
                default:
                    _packagesView.SortDescriptions.Add(new SortDescription("FullName", ListSortDirection.Ascending));
                    break;
            }
        }
    }

    // expose view for binding if needed
    public ICollectionView PackagesView => _packagesView;

    [RelayCommand]
    private async Task LoadPackagesAsync()
    {
        await ExecuteAsync(async () =>
        {
            IsLoading = true;
            var list = await _api.GetPackagesAsync();
            Packages.Clear();
            foreach (var p in list)
            {
                Packages.Add(p);
                foreach (var cat in p.Categories)
                {
                    if (!Categories.Contains(cat)) Categories.Add(cat);
                }
            }
            ApplySort();
            IsLoading = false;
        }, "loading mods from thunderstore");
    }

    [RelayCommand]
    private void ApplyFilters()
    {
        // in a real app we'd work with CollectionViewSource; for now rely on xaml filtering via bindings.
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private async Task InstallPackageAsync(ThunderstorePackage? package)
    {
        if (package == null || _gameInstall == null) return;
        var latest = package.Versions.FirstOrDefault(v => v.IsActive) ?? package.Versions.First();
        var result = await _modManager.InstallModFromZip(latest.DownloadUrl, _gameInstall);
        if (result.Success)
        {
            _snackbar.Show("Installed", $"{package.FullName} installed successfully", "Success", "✅", TimeSpan.FromSeconds(3));
        }
        else
        {
            _snackbar.Show("Install failed", result.Error ?? "Unknown error", "Error", "❌", TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private async Task ShowDetailsAsync(ThunderstorePackage? package)
    {
        if (package == null) return;
        await _dialogService.ShowModDetailsAsync(package);
    }
}

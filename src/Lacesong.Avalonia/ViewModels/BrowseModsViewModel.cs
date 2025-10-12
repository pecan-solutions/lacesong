using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Avalonia.Services;
using Lacesong.Avalonia.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;

namespace Lacesong.Avalonia.ViewModels;

public partial class BrowseModsViewModel : BaseViewModel
{
    private readonly IModManager _modManager;
    private readonly IModIndexService _modIndexService;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IGameStateService _gameStateService;

    [ObservableProperty]
    private ObservableCollection<ModDisplayItem> _mods = new();

    [ObservableProperty]
    private ModDisplayItem? _selectedMod;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private ObservableCollection<string> _categories = new();

    [ObservableProperty]
    private ObservableCollection<string> _sortOptions = new() { "rating", "downloads", "date", "name" };

    [ObservableProperty]
    private string _selectedSortOption = "rating";

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private string? _installingModId;

    [ObservableProperty]
    private double _installProgress;

    public bool CanGoToPreviousPage => CurrentPage > 1;
    public bool CanGoToNextPage => CurrentPage < TotalPages;

    partial void OnSelectedSortOptionChanged(string value)
    {
        _ = LoadModsAsync();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        CurrentPage = 1; // reset to first page when changing category
        _ = LoadModsAsync();
    }

    public BrowseModsViewModel(
        ILogger<BrowseModsViewModel> logger,
        IModManager modManager,
        IModIndexService modIndexService,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        IGameStateService gameStateService) : base(logger)
    {
        _modManager = modManager;
        _modIndexService = modIndexService;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _gameStateService = gameStateService;
        
        Console.WriteLine("BrowseModsViewModel: Constructor called");
        
        // use Task.Run to avoid blocking the constructor, but ensure UI updates work
        _ = Task.Run(async () =>
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BrowseModsViewModel: Constructor async initialization failed: {ex.Message}");
                Console.WriteLine($"BrowseModsViewModel: Stack trace: {ex.StackTrace}");
            }
        });
    }

    private async Task InitializeAsync()
    {
        Console.WriteLine("BrowseModsViewModel: InitializeAsync started");
        try
        {
            await LoadCategoriesAsync();
            await LoadModsAsync();
            Console.WriteLine("BrowseModsViewModel: InitializeAsync completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BrowseModsViewModel: InitializeAsync failed with exception: {ex.Message}");
            Console.WriteLine($"BrowseModsViewModel: Stack trace: {ex.StackTrace}");
        }
    }

    private async Task LoadCategoriesAsync()
    {
        Console.WriteLine("BrowseModsViewModel: LoadCategoriesAsync started");
        await ExecuteAsync(async () =>
        {
            Console.WriteLine("BrowseModsViewModel: Calling _modIndexService.GetCategories()");
            var cats = await _modIndexService.GetCategories();
            Console.WriteLine($"BrowseModsViewModel: Received {cats.Count} categories");
            Categories.Clear();
            Categories.Add("All");
            foreach (var cat in cats.OrderBy(c => c))
            {
                Console.WriteLine($"BrowseModsViewModel: Adding category: {cat}");
                Categories.Add(cat);
            }
            Console.WriteLine($"BrowseModsViewModel: Total categories in collection: {Categories.Count}");
        }, "Loading categories...");
    }

    [RelayCommand]
    private async Task LoadModsAsync()
    {
        await ExecuteAsync(async () =>
        {
            var criteria = new ModSearchCriteria
            {
                Query = string.IsNullOrEmpty(SearchText) ? null : SearchText,
                Category = SelectedCategory == "All" ? null : SelectedCategory,
                Page = CurrentPage,
                PageSize = 20,
                SortBy = SelectedSortOption,
                SortOrder = "desc"
            };

            Console.WriteLine($"BrowseModsViewModel: Searching with criteria - Query: {criteria.Query}, Category: {criteria.Category}, Page: {criteria.Page}");
            
            var results = await _modIndexService.SearchMods(criteria);
            Console.WriteLine($"BrowseModsViewModel: Received {results.Mods.Count} mods from service (Total: {results.TotalCount})");
            
            Mods.Clear();
            foreach (var mod in results.Mods)
            {
                Console.WriteLine($"BrowseModsViewModel: Adding mod - {mod.Name} by {mod.Author}");
                Mods.Add(new ModDisplayItem(mod));
            }
            
            Console.WriteLine($"BrowseModsViewModel: Added {Mods.Count} mods to collection");
            
            TotalPages = results.TotalPages;
            OnPropertyChanged(nameof(CanGoToPreviousPage));
            OnPropertyChanged(nameof(CanGoToNextPage));
        }, "Loading mods...");
    }
    
    [RelayCommand]
    private async Task InstallModAsync(ModDisplayItem modDisplay)
    {
        if (modDisplay == null || _gameStateService.CurrentGame == null) return;

        var mod = modDisplay.ModEntry;
        InstallingModId = mod.Id;
        InstallProgress = 0;
        var progress = new Progress<double>(p => InstallProgress = p);
        
        var cts = new CancellationTokenSource();

        var result = await ExecuteAsync(async () =>
        {
            var latestVersion = mod.Versions.Where(v => !v.IsPrerelease).OrderByDescending(v => v.ReleaseDate).FirstOrDefault();
            if (latestVersion == null) throw new("no stable version available");
            var op = await _modManager.InstallModFromZip(latestVersion.DownloadUrl, _gameStateService.CurrentGame, progress, cts.Token);
            return op;
        }, "Installing mod...");

        InstallingModId = null;
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            _ = LoadModsAsync();
            OnPropertyChanged(nameof(CanGoToPreviousPage));
            OnPropertyChanged(nameof(CanGoToNextPage));
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            _ = LoadModsAsync();
            OnPropertyChanged(nameof(CanGoToPreviousPage));
            OnPropertyChanged(nameof(CanGoToNextPage));
        }
    }
}

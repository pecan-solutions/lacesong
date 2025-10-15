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
using System.Collections.Generic;

namespace Lacesong.Avalonia.ViewModels;

public partial class BrowseModsViewModel : BaseViewModel
{
    private readonly IModManager _modManager;
    private readonly IModIndexService _modIndexService;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IGameStateService _gameStateService;
    private CancellationTokenSource? _searchDebounceTokenSource;
    private const int SearchDebounceDelayMs = 500;

    [ObservableProperty]
    private ObservableCollection<ModDisplayItem> _mods = new();

    private ModDisplayItem? _selectedMod;
    private bool _isLoadingMods = false;

    public ModDisplayItem? SelectedMod
    {
        get => _selectedMod;
        set
        {
            // prevent listbox from clearing selection during page load
            if (value == null && _isLoadingMods && _selectedMod != null)
            {
                return;
            }
            
            if (SetProperty(ref _selectedMod, value))
            {
                // property changed successfully
            }
        }
    }

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

    partial void OnSearchTextChanged(string value)
    {
        // cancel any pending search
        _searchDebounceTokenSource?.Cancel();
        _searchDebounceTokenSource = new CancellationTokenSource();
        var token = _searchDebounceTokenSource.Token;

        // debounce search to avoid excessive api calls while typing
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SearchDebounceDelayMs, token);
                if (!token.IsCancellationRequested)
                {
                    CurrentPage = 1; // reset to first page when searching
                    await LoadModsAsync();
                }
            }
            catch (TaskCanceledException)
            {
                // expected when user continues typing
            }
        });
    }

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
            _isLoadingMods = true;
            try
            {
                // preserve the currently selected mod completely
                var previouslySelectedMod = SelectedMod;

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
                
                // recalculate total pages based on total result count
                TotalPages = (int)Math.Ceiling(results.TotalCount / 20.0);
                OnPropertyChanged(nameof(CanGoToPreviousPage));
                OnPropertyChanged(nameof(CanGoToNextPage));

                // restore the selected mod if it exists in the new collection, otherwise keep the previous selection
                if (previouslySelectedMod != null)
                {
                    var restoredSelection = Mods.FirstOrDefault(m => m.Id == previouslySelectedMod.Id);
                    if (restoredSelection != null)
                    {
                        SelectedMod = restoredSelection;
                        Console.WriteLine($"BrowseModsViewModel: Restored selection for mod on current page: {restoredSelection.Name}");
                    }
                    else
                    {
                        // keep the previously selected mod even though it's not on this page
                        SelectedMod = previouslySelectedMod;
                        Console.WriteLine($"BrowseModsViewModel: Keeping previous selection (not on current page): {previouslySelectedMod.Name}");
                    }
                }
            }
            finally
            {
                _isLoadingMods = false;
            }
        }, "Loading mods...");
    }
    
    [RelayCommand]
    private async Task InstallModAsync(ModDisplayItem modDisplay)
    {
        Console.WriteLine($"BrowseModsViewModel: InstallModAsync called for mod: {modDisplay?.ModEntry?.Name ?? "null"}");
        
        if (modDisplay == null) 
        {
            Console.WriteLine("BrowseModsViewModel: InstallModAsync - modDisplay is null, returning");
            return;
        }
        
        if (_gameStateService.CurrentGame == null || string.IsNullOrEmpty(_gameStateService.CurrentGame.InstallPath))
        {
            Console.WriteLine("BrowseModsViewModel: InstallModAsync - No current game detected");
            await _dialogService.ShowMessageDialogAsync("Game Not Detected", "Please select a valid game installation before installing mods.");
            return;
        }

        Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Current game: {_gameStateService.CurrentGame.Name} at {_gameStateService.CurrentGame.InstallPath}");

        try
        {
            var mod = modDisplay.ModEntry;
            Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Mod details - Name: {mod.Name}, Author: {mod.Author}, ID: {mod.Id}");
            Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Available versions: {mod.Versions.Count}");
            
            InstallingModId = mod.Id;
            InstallProgress = 0;
            var progress = new Progress<double>(p => 
            {
                InstallProgress = p;
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Progress: {p:P2}");
            });
            var cts = new CancellationTokenSource();

            var result = await ExecuteAsync(async () =>
            {
                Console.WriteLine("BrowseModsViewModel: InstallModAsync - Starting version selection");
                var latestVersion = mod.Versions.Where(v => !v.IsPrerelease).OrderByDescending(v => v.ReleaseDate).FirstOrDefault();
                if (latestVersion == null)
                {
                    Console.WriteLine("BrowseModsViewModel: InstallModAsync - No stable version found");
                    _snackbarService.Show("Error", "No stable version of the mod is available for download.", "Error");
                    return OperationResult.ErrorResult("No stable version available");
                }
                
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Selected version: {latestVersion.Version} from {latestVersion.ReleaseDate}");
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Download URL: {latestVersion.DownloadUrl}");
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Calling _modManager.InstallModFromZip");
                
                var op = await _modManager.InstallModFromZip(latestVersion.DownloadUrl, _gameStateService.CurrentGame, progress, cts.Token);
                
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - InstallModFromZip result - Success: {op.Success}, Message: {op.Message}, Error: {op.Error}");
                return op;
            }, "Installing mod...");

            Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Final result - Success: {result.Success}");
            
            if (result.Success)
            {
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Installation successful, refreshing mod list");
                _snackbarService.Show("Success", $"Mod '{mod.Name}' installed successfully.", "Success");
                await LoadModsAsync(); // Refresh the list to remove the installed mod
                Console.WriteLine("BrowseModsViewModel: InstallModAsync - Mod list refreshed");
            }
            else
            {
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Installation failed: {result.Error}");
                _snackbarService.Show("Installation Failed", result.Error, "Error");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Exception occurred: {ex.Message}");
            Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Stack trace: {ex.StackTrace}");
            _snackbarService.Show("Error", $"An unexpected error occurred: {ex.Message}", "Error");
        }
        finally
        {
            Console.WriteLine("BrowseModsViewModel: InstallModAsync - Cleaning up");
            InstallingModId = null;
        }
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

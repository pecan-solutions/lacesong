using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Core.Services;
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
using System.IO;

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
    
    // efficient tracking of installed mods by name (for Thunderstore matching)
    private HashSet<string> _installedModNames = new();
    
    // public property for binding
    public HashSet<string> InstalledModNames => _installedModNames;

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
            await RefreshInstalledModsAsync();
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
                // refresh installed mods tracking first
                await RefreshInstalledModsAsync();
                
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
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Owner: {mod.Author}");
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Calling _modManager.InstallModFromZip");
                
                var op = await _modManager.InstallModFromZip(latestVersion.DownloadUrl, _gameStateService.CurrentGame, progress, cts.Token, mod.Author);
                
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - InstallModFromZip result - Success: {op.Success}, Message: {op.Message}, Error: {op.Error}");
                return op;
            }, "Installing mod...");

            Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Final result - Success: {result.Success}");
            
            if (result.Success)
            {
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Installation successful, updating installed mods list");
                _snackbarService.Show("Success", $"Mod '{mod.Name}' installed successfully.", "Success");
                
                // add to installed mods tracking by name
                _installedModNames.Add(mod.Name);
                
                // refresh the mod list to update button states
                await LoadModsAsync();
                Console.WriteLine("BrowseModsViewModel: InstallModAsync - Mod list refreshed");
            }
            else
            {
                Console.WriteLine($"BrowseModsViewModel: InstallModAsync - Installation failed: {result.Error}");
                _snackbarService.Show("Installation Failed", result.Error ?? "Unknown error occurred", "Error");
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

    [RelayCommand]
    private async Task UninstallModAsync(ModDisplayItem modDisplay)
    {
        Console.WriteLine($"BrowseModsViewModel: UninstallModAsync called for mod: {modDisplay?.ModEntry?.Name ?? "null"}");
        
        if (modDisplay == null) 
        {
            Console.WriteLine("BrowseModsViewModel: UninstallModAsync - modDisplay is null, returning");
            return;
        }
        
        if (_gameStateService.CurrentGame == null || string.IsNullOrEmpty(_gameStateService.CurrentGame.InstallPath))
        {
            Console.WriteLine("BrowseModsViewModel: UninstallModAsync - No current game detected");
            await _dialogService.ShowMessageDialogAsync("Game Not Detected", "Please select a valid game installation before uninstalling mods.");
            return;
        }

        var confirmResult = await _dialogService.ShowConfirmationDialogAsync(
            "Uninstall Mod",
            $"Are you sure you want to uninstall '{modDisplay.Name}'? This action cannot be undone.");
            
        if (!confirmResult) return;

        Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Current game: {_gameStateService.CurrentGame.Name} at {_gameStateService.CurrentGame.InstallPath}");

        try
        {
            var mod = modDisplay.ModEntry;
            Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Mod details - Name: {mod.Name}, Author: {mod.Author}, Thunderstore ID: {mod.Id}");
            
            // find the actual installed mod ID by name
            var actualModId = await FindModIdByNameAsync(mod.Name);
            if (string.IsNullOrEmpty(actualModId))
            {
                Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Could not find installed mod ID for: {mod.Name}");
                _snackbarService.Show("Error", $"Could not find installed mod '{mod.Name}'. It may have been manually removed.", "Error");
                return;
            }
            
            Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Found actual mod ID: {actualModId} for mod: {mod.Name}");
            
            InstallingModId = mod.Id; // use Thunderstore ID for progress tracking
            InstallProgress = 0;
            var progress = new Progress<double>(p => 
            {
                InstallProgress = p;
                Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Progress: {p:P2}");
            });

            var result = await ExecuteAsync(async () =>
            {
                Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Calling _modManager.UninstallMod with ID: {actualModId}");
                
                var op = await _modManager.UninstallMod(actualModId, _gameStateService.CurrentGame);
                
                Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - UninstallMod result - Success: {op.Success}, Message: {op.Message}, Error: {op.Error}");
                return op;
            }, "Uninstalling mod...");

            Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Final result - Success: {result.Success}");
            
            if (result.Success)
            {
                Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Uninstallation successful, updating installed mods list");
                _snackbarService.Show("Success", $"Mod '{mod.Name}' uninstalled successfully.", "Success");
                
                // remove from installed mods tracking by name
                _installedModNames.Remove(mod.Name);
                
                // refresh the mod list to update button states
                await LoadModsAsync();
                Console.WriteLine("BrowseModsViewModel: UninstallModAsync - Mod list refreshed");
            }
            else
            {
                Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Uninstallation failed: {result.Error}");
                _snackbarService.Show("Uninstallation Failed", result.Error ?? "Unknown error occurred", "Error");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Exception occurred: {ex.Message}");
            Console.WriteLine($"BrowseModsViewModel: UninstallModAsync - Stack trace: {ex.StackTrace}");
            _snackbarService.Show("Error", $"An unexpected error occurred: {ex.Message}", "Error");
        }
        finally
        {
            Console.WriteLine("BrowseModsViewModel: UninstallModAsync - Cleaning up");
            InstallingModId = null;
        }
    }

    // method to find the actual mod ID (directory name) by mod name
    private async Task<string?> FindModIdByNameAsync(string modName)
    {
        if (_gameStateService.CurrentGame == null || string.IsNullOrEmpty(_gameStateService.CurrentGame.InstallPath))
            return null;

        try
        {
            var modsDirectory = ModManager.GetModsDirectoryPath(_gameStateService.CurrentGame);
            if (!Directory.Exists(modsDirectory))
                return null;

            var modDirectories = Directory.GetDirectories(modsDirectory);
            
            foreach (var modDir in modDirectories)
            {
                var manifestPath = Path.Combine(modDir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var manifestContent = await File.ReadAllTextAsync(manifestPath);
                        var manifest = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(manifestContent);
                        
                        if (manifest.TryGetProperty("name", out var nameElement))
                        {
                            var currentModName = nameElement.GetString();
                            if (string.Equals(currentModName, modName, StringComparison.OrdinalIgnoreCase))
                            {
                                // return the directory name as the mod ID
                                return Path.GetFileName(modDir);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"BrowseModsViewModel: Error parsing manifest.json in {modDir}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BrowseModsViewModel: Error finding mod ID by name: {ex.Message}");
        }

        return null;
    }

    // helper method to check if a mod is installed by name
    public bool IsModInstalled(string modName)
    {
        return _installedModNames.Contains(modName);
    }

    // method to refresh installed mods tracking by scanning manifest.json files
    private async Task RefreshInstalledModsAsync()
    {
        if (_gameStateService.CurrentGame == null || string.IsNullOrEmpty(_gameStateService.CurrentGame.InstallPath))
        {
            _installedModNames.Clear();
            return;
        }

        try
        {
            _installedModNames.Clear();
            
            // get the mods directory path (handles cross-platform differences)
            var modsDirectory = ModManager.GetModsDirectoryPath(_gameStateService.CurrentGame);
            Console.WriteLine($"BrowseModsViewModel: Scanning mods directory: {modsDirectory}");
            
            if (!Directory.Exists(modsDirectory))
            {
                Console.WriteLine("BrowseModsViewModel: Mods directory does not exist");
                return;
            }

            // scan all mod directories for manifest.json files
            var modDirectories = Directory.GetDirectories(modsDirectory);
            Console.WriteLine($"BrowseModsViewModel: Found {modDirectories.Length} mod directories");
            
            foreach (var modDir in modDirectories)
            {
                var manifestPath = Path.Combine(modDir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        // efficiently read and parse just the name field
                        var manifestContent = await File.ReadAllTextAsync(manifestPath);
                        var manifest = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(manifestContent);
                        
                        if (manifest.TryGetProperty("name", out var nameElement))
                        {
                            var modName = nameElement.GetString();
                            if (!string.IsNullOrEmpty(modName))
                            {
                                _installedModNames.Add(modName);
                                Console.WriteLine($"BrowseModsViewModel: Found installed mod: {modName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"BrowseModsViewModel: Error parsing manifest.json in {modDir}: {ex.Message}");
                    }
                }
            }
            
            Console.WriteLine($"BrowseModsViewModel: Refreshed installed mods tracking - {_installedModNames.Count} mods installed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BrowseModsViewModel: Failed to refresh installed mods: {ex.Message}");
            _installedModNames.Clear();
        }
    }
}

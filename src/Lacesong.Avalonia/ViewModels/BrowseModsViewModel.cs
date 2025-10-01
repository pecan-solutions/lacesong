using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Interfaces;
using Lacesong.Core.Models;
using Lacesong.Avalonia.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.ViewModels;

public partial class BrowseModsViewModel : BaseViewModel
{
    private readonly IModManager _modManager;
    private readonly IModIndexService _modIndexService;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IGameStateService _gameStateService;

    [ObservableProperty]
    private ObservableCollection<ModIndexEntry> _mods = new();

    [ObservableProperty]
    private ModIndexEntry? _selectedMod;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private ObservableCollection<string> _categories = new();

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    public bool CanGoToPreviousPage => CurrentPage > 1;
    public bool CanGoToNextPage => CurrentPage < TotalPages;

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
        
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadCategoriesAsync();
        await LoadModsAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        await ExecuteAsync(async () =>
        {
            var cats = await _modIndexService.GetCategories();
            Categories.Clear();
            Categories.Add("All");
            foreach (var cat in cats.OrderBy(c => c))
            {
                Categories.Add(cat);
            }
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
                PageSize = 20
            };

            var results = await _modIndexService.SearchMods(criteria);
            
            Mods.Clear();
            foreach (var mod in results.Mods)
            {
                Mods.Add(mod);
            }
            
            TotalPages = results.TotalPages;
            OnPropertyChanged(nameof(CanGoToPreviousPage));
            OnPropertyChanged(nameof(CanGoToNextPage));
        }, "Loading mods...");
    }
    
    [RelayCommand]
    private async Task InstallModAsync(ModIndexEntry mod)
    {
        if (mod == null || _gameStateService.CurrentGame == null) return;
        
        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Install Mod",
            $"Are you sure you want to install '{mod.Name}' by {mod.Author}?");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            var latestVersion = mod.Versions
                .Where(v => !v.IsPrerelease)
                .OrderByDescending(v => v.ReleaseDate)
                .FirstOrDefault();
                
            if (latestVersion == null)
            {
                SetStatus("No stable version available", true);
                return;
            }
            
            var installResult = await _modManager.InstallModFromZip(latestVersion.DownloadUrl, _gameStateService.CurrentGame);
            
            if (installResult.Success)
            {
                SetStatus("Mod installed successfully");
                _snackbarService.Show("Success", $"Successfully installed {mod.Name}.", "Success");
            }
            else
            {
                SetStatus($"Installation failed: {installResult.Error}", true);
                _snackbarService.Show("Installation Failed", installResult.Error, "Error");
            }
        }, "Installing mod...");
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

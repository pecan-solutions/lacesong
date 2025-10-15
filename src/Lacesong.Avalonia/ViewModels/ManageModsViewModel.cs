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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Lacesong.Avalonia.ViewModels;

public partial class ManageModsViewModel : BaseViewModel
{
    private readonly IModManager _modManager;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IGameStateService _gameStateService;
    private CancellationTokenSource? _searchDebounceTokenSource;
    private const int SearchDebounceDelayMs = 500;

    [ObservableProperty]
    private ObservableCollection<ModInfo> _mods = new();

    [ObservableProperty]
    private ModInfo? _selectedMod;

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        // cancel any pending search
        _searchDebounceTokenSource?.Cancel();
        _searchDebounceTokenSource = new CancellationTokenSource();
        var token = _searchDebounceTokenSource.Token;

        // debounce search to avoid excessive filtering while typing
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SearchDebounceDelayMs, token);
                if (!token.IsCancellationRequested)
                {
                    await FilterModsAsync();
                }
            }
            catch (TaskCanceledException)
            {
                // expected when user continues typing
            }
        });
    }

    public ManageModsViewModel(
        ILogger<ManageModsViewModel> logger,
        IModManager modManager,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        IGameStateService gameStateService) : base(logger)
    {
        _modManager = modManager;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _gameStateService = gameStateService;
        
        _gameStateService.GameStateChanged += OnGameStateChanged;

        _ = Task.Run(async () =>
        {
            try
            {
                await LoadModsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ManageModsViewModel: Constructor async initialization failed: {ex.Message}");
            }
        });
    }

    private void OnGameStateChanged()
    {
        _ = LoadModsAsync();
    }

    [RelayCommand]
    private async Task LoadModsAsync()
    {
        if (_gameStateService.CurrentGame == null || string.IsNullOrEmpty(_gameStateService.CurrentGame.InstallPath))
        {
            Mods.Clear();
            return;
        }

        await ExecuteAsync(async () =>
        {
            var installedMods = await _modManager.GetInstalledMods(_gameStateService.CurrentGame);
            
            Mods.Clear();
            foreach (var mod in installedMods.OrderBy(m => m.Name))
            {
                Mods.Add(mod);
            }
            
            SetStatus($"Loaded {installedMods.Count} installed mods");
        }, "Loading mods...");
    }

    private async Task FilterModsAsync()
    {
        if (_gameStateService.CurrentGame == null || string.IsNullOrEmpty(_gameStateService.CurrentGame.InstallPath))
        {
            Mods.Clear();
            return;
        }

        await ExecuteAsync(async () =>
        {
            var installedMods = await _modManager.GetInstalledMods(_gameStateService.CurrentGame);
            
            var filtered = installedMods;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = installedMods.Where(m => 
                    m.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (m.Author?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (m.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }
            
            Mods.Clear();
            foreach (var mod in filtered.OrderBy(m => m.Name))
            {
                Mods.Add(mod);
            }
            
            SetStatus($"Found {filtered.Count} mod(s)");
        }, "Filtering mods...");
    }

    [RelayCommand]
    private async Task UninstallModAsync(ModInfo mod)
    {
        if (mod == null || _gameStateService.CurrentGame == null) return;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Uninstall Mod",
            $"Are you sure you want to uninstall '{mod.Name}'? This action cannot be undone.");
            
        if (!result) return;

        await ExecuteAsync(async () =>
        {
            var uninstallResult = await _modManager.UninstallMod(mod.Id, _gameStateService.CurrentGame);
            
            if (uninstallResult.Success)
            {
                SetStatus("Mod uninstalled successfully");
                _snackbarService.Show(
                    "Success", 
                    $"Successfully uninstalled {mod.Name}.", 
                    "Success");
                await LoadModsAsync();
            }
            else
            {
                SetStatus($"Uninstallation failed: {uninstallResult.Error}", true);
                _snackbarService.Show(
                    "Uninstallation Failed", 
                    uninstallResult.Error, 
                    "Error");
            }
        }, "Uninstalling mod...");
    }

    [RelayCommand]
    private void OpenModsFolder()
    {
        if (_gameStateService.CurrentGame == null) return;
        
        try
        {
            var modsPath = ModManager.GetModsDirectoryPath(_gameStateService.CurrentGame);
            if (!Directory.Exists(modsPath))
            {
                Directory.CreateDirectory(modsPath);
            }
            
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = modsPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SetStatus($"failed to open mods folder: {ex.Message}", true);
            _snackbarService.Show(
                "Error", 
                $"Failed to open mods folder: {ex.Message}", 
                "Error");
        }
    }

    [RelayCommand]
    private void OpenWebsite(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                SetStatus("invalid url scheme; only http/https allowed", true);
                _snackbarService.Show(
                    "Error",
                    "Invalid URL. Only http and https schemes are supported.",
                    "Error");
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri.ToString(),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SetStatus($"failed to open website: {ex.Message}", true);
            _snackbarService.Show(
                "Error", 
                $"Failed to open website: {ex.Message}", 
                "Error");
        }
    }
}


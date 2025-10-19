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
        Console.WriteLine($"=== LOAD MODS ASYNC DEBUG START ===");
        Console.WriteLine($"LoadModsAsync called");
        
        if (_gameStateService.CurrentGame == null || string.IsNullOrEmpty(_gameStateService.CurrentGame.InstallPath))
        {
            Console.WriteLine($"No current game or install path, clearing mods");
            Mods.Clear();
            return;
        }

        Console.WriteLine($"Current game: {_gameStateService.CurrentGame.Name}");
        Console.WriteLine($"Install path: {_gameStateService.CurrentGame.InstallPath}");

        await ExecuteAsync(async () =>
        {
            Console.WriteLine($"Calling _modManager.GetInstalledMods");
            var installedMods = await _modManager.GetInstalledMods(_gameStateService.CurrentGame);
            Console.WriteLine($"GetInstalledMods returned {installedMods.Count} mods");
            
            // log each mod's status
            foreach (var mod in installedMods)
            {
                Console.WriteLine($"Mod: {mod.Name} (ID: {mod.Id}), IsEnabled: {mod.IsEnabled}");
            }
            
            Mods.Clear();
            foreach (var mod in installedMods.OrderBy(m => m.DisplayName))
            {
                Mods.Add(mod);
            }
            
            SetStatus($"Loaded {installedMods.Count} installed mods");
            Console.WriteLine($"Added {installedMods.Count} mods to UI collection");
        }, "Loading mods...");
        
        Console.WriteLine($"=== LOAD MODS ASYNC DEBUG END ===");
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
                    m.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (m.Author?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (m.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }
            
            Mods.Clear();
            foreach (var mod in filtered.OrderBy(m => m.DisplayName))
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
            var uninstallResult = await _modManager.UninstallMod(mod.DirectoryName ?? mod.Id, _gameStateService.CurrentGame);
            
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
    private async Task EnableModAsync(ModInfo mod)
    {
        if (mod == null || _gameStateService.CurrentGame == null) return;

        Console.WriteLine($"=== ENABLE MOD ASYNC DEBUG START ===");
        Console.WriteLine($"EnableModAsync called for mod: {mod.Name} (ID: {mod.Id})");
        Console.WriteLine($"Current mod IsEnabled: {mod.IsEnabled}");
        Console.WriteLine($"Game: {_gameStateService.CurrentGame.Name}");

        await ExecuteAsync(async () =>
        {
            Console.WriteLine($"Calling _modManager.EnableMod for modId: {mod.Id}");
            var result = await _modManager.EnableMod(mod.DirectoryName ?? mod.Id, _gameStateService.CurrentGame);

            Console.WriteLine($"EnableMod result - Success: {result.Success}, Message: {result.Message}");
            if (!string.IsNullOrEmpty(result.Error))
            {
                Console.WriteLine($"EnableMod result - Error: {result.Error}");
            }

            if (result.Success)
            {
                SetStatus($"{mod.Name} enabled");
                _snackbarService.Show("Success", $"Enabled {mod.Name}.", "Success");
                Console.WriteLine($"Success toast shown, now calling LoadModsAsync");
                await LoadModsAsync();
                Console.WriteLine($"LoadModsAsync completed");
            }
            else
            {
                SetStatus(result.Error, true);
                _snackbarService.Show("Error", result.Error, "Error");
                Console.WriteLine($"Error toast shown");
            }
        }, "Enabling mod...");
        
        Console.WriteLine($"=== ENABLE MOD ASYNC DEBUG END ===");
    }

    [RelayCommand]
    private async Task DisableModAsync(ModInfo mod)
    {
        if (mod == null || _gameStateService.CurrentGame == null) return;

        await ExecuteAsync(async () =>
        {
            var result = await _modManager.DisableMod(mod.DirectoryName ?? mod.Id, _gameStateService.CurrentGame);

            if (result.Success)
            {
                SetStatus($"{mod.Name} disabled");
                _snackbarService.Show("Success", $"Disabled {mod.Name}.", "Success");
                await LoadModsAsync();
            }
            else
            {
                SetStatus(result.Error, true);
                _snackbarService.Show("Error", result.Error, "Error");
            }
        }, "Disabling mod...");
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


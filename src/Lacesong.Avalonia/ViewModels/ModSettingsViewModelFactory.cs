using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Lacesong.Core.Models;
using Lacesong.Core.Interfaces;

namespace Lacesong.Avalonia.ViewModels;

public partial class ModSettingsViewModelFactory
{
    private readonly ILogger<ModSettingsViewModel> _logger;
    private readonly IModUpdateService _modUpdateService;

    public ModSettingsViewModelFactory(ILogger<ModSettingsViewModel> logger, IModUpdateService modUpdateService)
    {
        _logger = logger;
        _modUpdateService = modUpdateService;
    }

    public ModSettingsViewModel Create(ModInfo modInfo)
    {
        var viewModel = new ModSettingsViewModel(_logger, _modUpdateService);
        // In a real implementation, you would pass the modInfo to the view model
        // For now, this is a placeholder.
        return viewModel;
    }
}

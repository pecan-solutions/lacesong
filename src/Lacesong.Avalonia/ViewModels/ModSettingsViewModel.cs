using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lacesong.Core.Models;
using Lacesong.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.ViewModels;

public partial class ModSettingsViewModel : BaseViewModel
{
    private readonly IModUpdateService _updateService;
    private GameInstallation _gameInstall;

    public string ModId { get; private set; }

    [ObservableProperty]
    private bool _autoUpdateEnabled;

    [ObservableProperty]
    private string _updateChannel = "stable"; // stable, beta, alpha

    [ObservableProperty]
    private bool _preserveConfigs = true;

    [ObservableProperty]
    private bool _backupBeforeUpdate = true;

    [ObservableProperty]
    private string _status = string.Empty;

    public ModSettingsViewModel(
        ILogger<ModSettingsViewModel> logger,
        IModUpdateService updateService) : base(logger)
    {
        _updateService = updateService;
    }

    public async Task LoadAsync(GameInstallation installation, string modId)
    {
        _gameInstall = installation;
        ModId = modId;

        var settings = await _updateService.GetUpdateSettings(ModId, _gameInstall);
        AutoUpdateEnabled = settings.AutoUpdateEnabled;
        UpdateChannel = settings.UpdateChannel;
        PreserveConfigs = settings.PreserveConfigs;
        BackupBeforeUpdate = settings.BackupBeforeUpdate;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Status = "saving settings...";
        var settings = new ModUpdateSettings
        {
            ModId = ModId,
            AutoUpdateEnabled = AutoUpdateEnabled,
            UpdateChannel = UpdateChannel,
            PreserveConfigs = PreserveConfigs,
            BackupBeforeUpdate = BackupBeforeUpdate
        };
        var result = await _updateService.SetUpdateSettings(settings, _gameInstall);
        Status = result.Success ? "settings saved" : $"failed: {result.Error}";
    }
}

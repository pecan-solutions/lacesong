using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lacesong.Avalonia.ViewModels;

/// <summary>
/// base view model with common functionality
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    protected readonly ILogger Logger;

    protected BaseViewModel(ILogger logger)
    {
        Logger = logger;
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    protected void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        HasError = isError;
        ErrorMessage = isError ? message : string.Empty;
        
        if (isError)
        {
            Logger.LogError(message);
        }
        else
        {
            Logger.LogInformation(message);
        }
    }

    protected void ClearStatus()
    {
        StatusMessage = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;
    }

    protected async Task ExecuteAsync(Func<Task> operation, string? statusMessage = null)
    {
        try
        {
            IsBusy = true;
            ClearStatus();
            
            if (!string.IsNullOrEmpty(statusMessage))
            {
                SetStatus(statusMessage);
            }

            await operation();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string? statusMessage = null)
    {
        try
        {
            IsBusy = true;
            ClearStatus();
            
            if (!string.IsNullOrEmpty(statusMessage))
            {
                SetStatus(statusMessage);
            }

            return await operation();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", true);
            return default(T)!;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

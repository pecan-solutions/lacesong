using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lacesong.WPF.ViewModels;

/// <summary>
/// base view model with common functionality
/// </summary>
public abstract class BaseViewModel : ObservableObject
{
    protected readonly ILogger Logger;

    protected BaseViewModel(ILogger logger)
    {
        Logger = logger;
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

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

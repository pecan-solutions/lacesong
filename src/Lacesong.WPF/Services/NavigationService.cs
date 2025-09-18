using System;
using System.Threading.Tasks;

namespace Lacesong.WPF.Services;

/// <summary>
/// interface for navigation service
/// </summary>
public interface INavigationService
{
    void NavigateTo(string viewName);
    Task NavigateToAsync(string viewName);
}

/// <summary>
/// simple implementation of navigation service
/// </summary>
public class NavigationService : INavigationService
{
    public void NavigateTo(string viewName)
    {
        // simple implementation - could be enhanced with proper navigation
        System.Diagnostics.Debug.WriteLine($"Navigate to: {viewName}");
    }

    public async Task NavigateToAsync(string viewName)
    {
        NavigateTo(viewName);
        await Task.CompletedTask;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lacesong.Avalonia.Services;

public class ThemeService : IThemeService
{
    private readonly ISettingsService _settingsService;
    
    public ThemeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }
    
    public IEnumerable<string> GetAvailableThemes()
    {
        return new[] { "The Marrow", "Moss Grotto", "Haunted Bellhart" };
    }

    public void SetTheme(string themeName)
    {
        if (Application.Current == null)
        {
            return;
        }

        var themeUri = themeName switch
        {
            "The Marrow" => new Uri("avares://Lacesong.Avalonia/Styles/Themes/TheMarrow.axaml"),
            "Moss Grotto" => new Uri("avares://Lacesong.Avalonia/Styles/Themes/MossGrotto.axaml"),
            "Haunted Bellhart" => new Uri("avares://Lacesong.Avalonia/Styles/Themes/HauntedBellhart.axaml"),
            _ => new Uri("avares://Lacesong.Avalonia/Styles/Themes/TheMarrow.axaml")
        };
        
        var themeInclude = new ResourceInclude(themeUri)
        {
            Source = themeUri
        };

        // find any previously applied theme resource include (located in /Themes/)
        var existingTheme = Application.Current.Resources.MergedDictionaries
            .OfType<ResourceInclude>()
            .FirstOrDefault(x => x.Source?.ToString().Contains("/Themes/") ?? false);

        if (existingTheme is not null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(existingTheme);
        }

        Application.Current.Resources.MergedDictionaries.Add(themeInclude);

        // persist selection
        _settingsService.CurrentSettings.Theme = themeName;
        _settingsService.SaveSettings();
    }
}

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
        
        var theme = new ResourceInclude(themeUri)
        {
            Source = themeUri
        };

        if (Application.Current.Resources.MergedDictionaries
                .OfType<ResourceInclude>()
                .FirstOrDefault(x => x.Source?.ToString().Contains("Base.axaml") ?? false)
            is { } baseResource)
        {
            if (baseResource.Loaded is ResourceDictionary themeDictionary)
            {
                var currentTheme = (ResourceDictionary)themeDictionary.MergedDictionaries[0];
                currentTheme.MergedDictionaries.Clear();
                currentTheme.MergedDictionaries.Add(theme);

                _settingsService.CurrentSettings.Theme = themeName;
                _settingsService.SaveSettings();
            }
        }
    }
}

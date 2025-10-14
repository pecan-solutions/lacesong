using System;
using System.Collections.Generic;

namespace Lacesong.Avalonia.Services;

public interface IThemeService
{
    IEnumerable<string> GetAvailableThemes();
    void SetTheme(string themeName);
}

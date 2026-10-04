using System.Collections.Generic;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

public sealed class ThemeDefaults(IReadOnlyDictionary<string, Color> colours) : IThemeDefaults
{
    private readonly IReadOnlyDictionary<string, Color> _colours = colours;

    public bool TryGetColor(string key, out Color color) => _colours.TryGetValue(key, out color);
}

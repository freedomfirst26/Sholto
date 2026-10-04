using System;
using System.IO;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Builds <see cref="ThemeDefaults"/> from the real bundled defaults.json.</summary>
internal sealed class TestThemeDefaults
{
    public ThemeDefaults Create()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "src", "Sholto.Interface.MainUI", "Themes", "defaults.json")))
            dir = Path.GetDirectoryName(dir) ?? throw new FileNotFoundException("defaults.json");
        var json = File.ReadAllText(Path.Combine(dir, "src", "Sholto.Interface.MainUI", "Themes", "defaults.json"));
        return new ThemeDefaults(new ThemeDefaultsJson().Parse(json));
    }
}

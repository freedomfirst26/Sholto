using System;
using System.IO;
using Avalonia.Platform;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Constructs the theme collaborators (presets, palette factories, JSON
/// loader, catalog factory, context). Only the construction lives here; the call to
/// <see cref="Build"/> stays in <c>App.OnFrameworkInitializationCompleted</c>, because
/// the bundled theme data (<c>waveform-presets.json</c>, <c>defaults.json</c>, the themes)
/// is loaded through Avalonia's <c>AssetLoader</c>, which needs a live application.</summary>
public sealed class ThemeStackFactory : IThemeStackFactory
{
    public ThemeStack Build()
    {
        IWaveformPresets presets = new WaveformPresets(
            new WaveformPresetCatalogueFactory().Create(ReadAsset("waveform-presets.json")));
        IThemeDefaults defaults = new ThemeDefaults(
            new ThemeDefaultColoursFactory().Create(ReadAsset("defaults.json")));
        var catalog = new ThemeCatalogFactory(
            new SholtoThemeJson(
                new SholtoThemeFactory(new WaveformPaletteFactory(presets, defaults), new MinimapPaletteFactory(), defaults))).Build();
        var context = new ThemeContext(catalog);
        return new ThemeStack(catalog, context, defaults);
    }

    private string ReadAsset(string name)
    {
        using var s = AssetLoader.Open(new Uri($"avares://Sholto.Interface.MainUI/Themes/{name}"));
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}

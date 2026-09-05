using System.IO;
using System.Linq;
using Avalonia.Media;
using Sholto.App.Theming;

namespace Sholto.App.Tests;

public class WaveformPaletteTests
{
    private static readonly Color BgDeep     = Color.Parse("#101010");
    private static readonly Color Accent     = Color.Parse("#FF4E9A");
    private static readonly Color Mint       = Color.Parse("#34F0C6");
    private static readonly Color TextBright = Color.Parse("#EEEAFF");
    private static readonly Color TextMuted  = Color.Parse("#8B7FB8");

    [Fact]
    public void Derive_UsesThreeBandScheme_ForEveryPreset()
    {
        // Today every theme draws the Rekordbox 3-band scheme (ForceThreeBand);
        // the derived default must preserve that so no theme changes look.
        foreach (WaveformPreset p in Enum.GetValues<WaveformPreset>())
        {
            var w = WaveformPalette.DeriveFrom(p, BgDeep, Accent, Mint, TextBright, TextMuted);
            Assert.Equal(Color.Parse("#2A7FFF"), w.Low);
            Assert.Equal(Color.Parse("#FF8C1A"), w.Mid);
        }
    }

    [Fact]
    public void Derive_TakesDownbeatFromPreset()
    {
        var hot   = WaveformPalette.DeriveFrom(WaveformPreset.Hot,   BgDeep, Accent, Mint, TextBright, TextMuted);
        var bands = WaveformPalette.DeriveFrom(WaveformPreset.Bands, BgDeep, Accent, Mint, TextBright, TextMuted);
        Assert.Equal(Color.FromArgb(0xC8, 0xFF, 0xD6, 0x3D), hot.Downbeat);
        Assert.Equal(Color.FromArgb(0xD8, 0xE6, 0xF0, 0xFF), bands.Downbeat);
    }

    [Fact]
    public void Derive_CoreColoursComeFromTheme()
    {
        var w = WaveformPalette.DeriveFrom(WaveformPreset.Bands, BgDeep, Accent, Mint, TextBright, TextMuted);
        Assert.Equal(Color.Parse("#111111"), w.Background);                       // fixed bake background (unchanged)
        Assert.Equal(Color.FromArgb(0xFF, Mint.R, Mint.G, Mint.B), w.Playhead);
        Assert.Equal(Color.FromArgb(0xFF, Mint.R, Mint.G, Mint.B), w.Gain);
        Assert.Equal(Color.FromArgb(0x80, Accent.R, Accent.G, Accent.B), w.Loop);
        Assert.Equal(Color.FromArgb(0xFF, Accent.R, Accent.G, Accent.B), w.Marker);
        Assert.Equal(Color.FromArgb(0xC0, TextBright.R, TextBright.G, TextBright.B), w.BeatTick);
    }

    [Fact]
    public void Presets_KeepTheirBandTable()
    {
        var (lo, mid, hi) = WaveformPresets.Bands(WaveformPreset.Hot);
        Assert.Equal(Color.Parse("#FF3D3D"), lo);
        Assert.Equal(Color.Parse("#3DFF7A"), mid);
        Assert.Equal(Color.Parse("#3D8BFF"), hi);
    }

    private static string ThemeJson(string preset, string? waveformSection) => $$"""
    {
      "name": "Test",
      "bgDeep":        "#101010",
      "surface":       "#202020",
      "surfaceRaised": "#303030",
      "border":        "#404040",
      "primary":       "#7C5CFF",
      "accent":        "#FF4E9A",
      "accentBg":      "#33FF4E9A",
      "mint":          "#34F0C6",
      "textBright":    "#EEEAFF",
      "textMuted":     "#8B7FB8",
      "playedFadeColor": "#101010",
      "waveformPalette": "{{preset}}",
      "camelotPalette": {
        "hueOffset": 0, "saturation": 0.8, "majorLightness": 0.55, "minorLightness": 0.42,
        "onChipForeground": "#101820"
      }
      {{(waveformSection is null ? "" : "," + waveformSection)}}
    }
    """;

    [Fact]
    public void Parse_ExplicitWaveformSection_YieldsExactColours()
    {
        var theme = SholtoThemeJson.Parse(ThemeJson("Bands", """
            "waveform": {
              "background": "#010203", "low": "#111111", "mid": "#222222",
              "downbeat": "#C8444444", "beatTick": "#C0555555", "playhead": "#666666",
              "marker": "#777777", "gain": "#C0999999", "loop": "#80AAAAAA"
            }
            """));
        var w = theme.Waveform;
        Assert.Equal(Color.Parse("#010203"),   w.Background);
        Assert.Equal(Color.Parse("#111111"),   w.Low);
        Assert.Equal(Color.Parse("#222222"),   w.Mid);
        Assert.Equal(Color.Parse("#C8444444"), w.Downbeat);
        Assert.Equal(Color.Parse("#C0555555"), w.BeatTick);
        Assert.Equal(Color.Parse("#666666"),   w.Playhead);
        Assert.Equal(Color.Parse("#777777"),   w.Marker);
        Assert.Equal(Color.Parse("#C0999999"), w.Gain);
        Assert.Equal(Color.Parse("#80AAAAAA"), w.Loop);
    }

    [Fact]
    public void Parse_NoWaveformSection_DerivesFromPresetAndTheme()
    {
        var theme = SholtoThemeJson.Parse(ThemeJson("Hot", null));
        Assert.Equal(WaveformPreset.Hot, theme.WaveformPreset);
        Assert.Equal(Color.FromArgb(0xC8, 0xFF, 0xD6, 0x3D), theme.Waveform.Downbeat);
        Assert.Equal(Color.Parse("#2A7FFF"), theme.Waveform.Low);
        Assert.Equal(Color.FromArgb(0xFF, 0x34, 0xF0, 0xC6), theme.Waveform.Playhead);
        Assert.Equal(Color.FromArgb(0x80, 0xFF, 0x4E, 0x9A), theme.Waveform.Loop);
    }

    [Fact]
    public void Parse_PartialSection_KeepsExplicitAndDerivesRest()
    {
        var theme = SholtoThemeJson.Parse(ThemeJson("Bands", """
            "waveform": { "marker": "#123456" }
            """));
        Assert.Equal(Color.Parse("#123456"), theme.Waveform.Marker);
        Assert.Equal(Color.Parse("#FF8C1A"), theme.Waveform.Mid);                          // derived 3-band
    }

    [Fact]
    public void Parse_UnknownPresetName_FallsBackToBands()
    {
        var theme = SholtoThemeJson.Parse(ThemeJson("NoSuchPreset", null));
        Assert.Equal(WaveformPreset.Bands, theme.WaveformPreset);
    }

    private static string ThemesDir()
    {
        // Walk up from the test bin dir to the repo root (the dir containing src/Sholto.App/Themes/themes.manifest).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Sholto.App", "Themes");
            if (File.Exists(Path.Combine(candidate, "themes.manifest"))) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate src/Sholto.App/Themes from " + AppContext.BaseDirectory);
    }

    private static IEnumerable<(string File, SholtoTheme Theme)> BundledThemes()
    {
        var dir = ThemesDir();
        foreach (var line in File.ReadAllLines(Path.Combine(dir, "themes.manifest")))
        {
            var name = line.Trim();
            if (name.Length == 0 || name.StartsWith('#')) continue;
            yield return (name, SholtoThemeJson.Parse(File.ReadAllText(Path.Combine(dir, name))));
        }
    }

    [Fact]
    public void AllBundledThemes_Parse_AndDeclareTheirOwnBands()
    {
        var themes = BundledThemes().ToList();
        Assert.NotEmpty(themes);
        foreach (var (file, t) in themes)
        {
            var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(ThemesDir(), file)));
            Assert.True(json.RootElement.TryGetProperty("waveform", out var wf), $"{file}: missing waveform section");
            Assert.True(wf.TryGetProperty("low", out var low), $"{file}: missing waveform.low");
            Assert.True(wf.TryGetProperty("mid", out var mid), $"{file}: missing waveform.mid");
            Assert.True(wf.TryGetProperty("downbeat", out var downbeat), $"{file}: missing waveform.downbeat");
            Assert.False(wf.TryGetProperty("high", out _), $"{file}: waveform.high should not exist — the inner band is fixed white");

            Assert.Equal(Color.Parse(low.GetString()!), t.Waveform.Low);
            Assert.Equal(Color.Parse(mid.GetString()!), t.Waveform.Mid);
            Assert.Equal(Color.Parse(downbeat.GetString()!), t.Waveform.Downbeat);
        }
    }

    [Fact]
    public void AllBundledThemes_WaveformPreset_IsDefinedEnumMember()
    {
        foreach (var (file, t) in BundledThemes())
        {
            Assert.True(Enum.IsDefined(t.WaveformPreset), $"{file}: WaveformPreset {t.WaveformPreset} is not a defined enum member");
        }
    }

    [Fact]
    public void Serato_UsesItsOwnBands()
    {
        var serato = BundledThemes().Single(x => x.File == "serato.json").Theme;
        Assert.Equal(Color.Parse("#FF3D3D"), serato.Waveform.Low);
    }
}

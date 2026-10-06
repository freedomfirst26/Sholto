using Avalonia.Media;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The knob palette comes from the theme: "knob.arc" (default green from defaults.json), the rest from
/// the theme's core colours.</summary>
public class KnobThemeTests
{
    public KnobThemeTests() => AvaloniaTestApp.EnsureStarted();

    [Fact]
    public void Every_bundled_theme_gets_the_default_green_arc_and_its_own_core_colours()
    {
        var stack = new ThemeStackFactory().Build();
        Assert.True(stack.Defaults.TryGetColor("knob.arc", out var green));
        Assert.Equal(Color.Parse("#3DDC84"), green);
        foreach (var theme in stack.Catalog.All.Where(t => !t.IsUser))
        {
            Assert.Equal(green, theme.Knob.Arc);
            Assert.Equal(((ISolidColorBrush)theme.Border).Color, theme.Knob.Track);
            Assert.Equal(((ISolidColorBrush)theme.SurfaceRaised).Color, theme.Knob.Cap);
            Assert.Equal(((ISolidColorBrush)theme.TextMuted).Color, theme.Knob.Tick);
            Assert.Equal(((ISolidColorBrush)theme.TextBright).Color, theme.Knob.Pointer);
        }
    }

    [Fact]
    public void A_theme_can_set_its_own_arc_colour()
    {
        var stack = new ThemeStackFactory().Build();
        var factory = new SholtoThemeFactory(
            new WaveformPaletteFactory(new TestWaveformPresets().Create(), stack.Defaults), new MinimapPaletteFactory(),
            stack.Defaults);
        var theme = factory.Create("""
            {
              "name": "Knob Test", "bgDeep": "#111111", "surface": "#1A1A1A", "surfaceRaised": "#222222",
              "border": "#333333", "primary": "#00FFCC", "accent": "#FFC700", "accentBg": "#33FFC700",
              "mint": "#FFFFFF", "textBright": "#EEEEEE", "textMuted": "#888888", "playedFadeColor": "#111111",
              "waveformPalette": "Bands",
              "camelotPalette": { "hueOffset": 0, "saturation": 0.78, "majorLightness": 0.55, "minorLightness": 0.42,
                                  "onChipForeground": "#101820" },
              "knob": { "arc": "#FF00AA" }
            }
            """);
        Assert.Equal(Color.Parse("#FF00AA"), theme.Knob.Arc);
    }
}

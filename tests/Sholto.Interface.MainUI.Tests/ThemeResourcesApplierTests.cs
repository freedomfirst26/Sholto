using Avalonia.Controls;
using Avalonia.Media;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Applying a theme publishes every "Sholto…" key as the new theme's value, and tells the tree once
/// instead of once per key (the Layout Wizard's theme step re-toned the app on every arrow key).</summary>
public sealed class ThemeResourcesApplierTests
{
    private static readonly string[] Keys =
    [
        "SholtoAccent", "SholtoAccentBg", "SholtoBgDeep", "SholtoBorder", "SholtoDeckShadow", "SholtoFaceplateGlow",
        "SholtoFaceplateHover", "SholtoFaceplateRest", "SholtoFaceplateSelected", "SholtoIconPlate",
        "SholtoKeyChipForeground", "SholtoKeyChipGlow", "SholtoKnobPalette", "SholtoMinimapPalette", "SholtoMint",
        "SholtoMute", "SholtoPrimary", "SholtoPrimaryTint", "SholtoScrim", "SholtoShadow", "SholtoShadowColor",
        "SholtoSlotDeck1Wash", "SholtoSlotDeck2Wash", "SholtoSlotEmptyFill", "SholtoSlotHalo", "SholtoSlotSweep", "SholtoStatusAttention", "SholtoStatusError",
        "SholtoStatusErrorTint", "SholtoStatusOk", "SholtoStatusOkTint", "SholtoStatusWarn", "SholtoStatusWarnTint",
        "SholtoStemChipForeground", "SholtoStemDrums", "SholtoStemInstrumental", "SholtoStemVocals", "SholtoSurface",
        "SholtoSurfaceRaised", "SholtoTagChipBackground", "SholtoTagChipForeground", "SholtoTagIndicatorBackground",
        "SholtoTagIndicatorForeground", "SholtoTextBright", "SholtoTextBrightColor", "SholtoTextMuted",
        "SholtoWarning", "SholtoWaveformPalette", "TagChipBackground", "TagChipForeground", "TagIndicatorBackground",
        "TagIndicatorForeground",
    ];

    private static object? Resolve(Control host, string key) =>
        host.TryGetResource(key, null, out var value) ? value : null;

    private static (SholtoTheme First, SholtoTheme Second) TwoThemes()
    {
        AvaloniaTestApp.EnsureStarted();
        var all = new ThemeStackFactory().Build().Catalog.All;
        return (all[0], all.First(t => !ReferenceEquals(t.Primary, all[0].Primary)));
    }

    [Fact]
    public void Apply_PublishesEveryKey()
    {
        var (first, _) = TwoThemes();
        var host = new Border();
        new ThemeResourcesApplier().Apply(host.Resources, first);
        foreach (var key in Keys) Assert.True(Resolve(host, key) is not null, $"{key} is not published");
    }

    [Fact]
    public void Apply_ASecondTheme_ReplacesTheFirstsValues()
    {
        var (first, second) = TwoThemes();
        var host = new Border();
        var applier = new ThemeResourcesApplier();
        applier.Apply(host.Resources, first);
        applier.Apply(host.Resources, second);
        foreach (var key in Keys) Assert.True(Resolve(host, key) is not null, $"{key} is not published");
        Assert.Same(second.Primary, Resolve(host, "SholtoPrimary"));
        Assert.Same(second.Surface, Resolve(host, "SholtoSurface"));
        Assert.Same(second.Knob, Resolve(host, "SholtoKnobPalette"));
        Assert.Same(second.Waveform, Resolve(host, "SholtoWaveformPalette"));
        Assert.Same(second.Minimap, Resolve(host, "SholtoMinimapPalette"));
        Assert.Same(second.CamelotPalette.KeyChipForeground, Resolve(host, "SholtoKeyChipForeground"));
        Assert.Equal(second.Stems.ChipText, ((Avalonia.Media.SolidColorBrush)Resolve(host, "SholtoStemChipForeground")!).Color);
        Assert.Single(host.Resources.MergedDictionaries);
    }

    [Fact]
    public void Apply_RaisesAtMostTwoResourcesChanged()
    {
        var (first, second) = TwoThemes();
        var host = new Border();
        var applier = new ThemeResourcesApplier();
        applier.Apply(host.Resources, first);
        var raised = 0;
        ((IResourceHost)host).ResourcesChanged += (_, _) => raised++;
        applier.Apply(host.Resources, second);
        Assert.InRange(raised, 1, 2);
    }

    [Fact]
    public void Apply_KeepsKeysTheHostSetItself()
    {
        var (first, second) = TwoThemes();
        var host = new Border();
        host.Resources["SholtoWaveformStyle"] = "style";
        var applier = new ThemeResourcesApplier();
        applier.Apply(host.Resources, first);
        applier.Apply(host.Resources, second);
        Assert.Equal("style", Resolve(host, "SholtoWaveformStyle"));
    }

    private static Color LoadColour(Control host, string key) => ((SolidColorBrush)Resolve(host, key)!).Color;

    [Fact]
    public void Apply_ThemeWithoutLoad_PublishesDefaultLoadColours()
    {
        var stack = new ThemeStackFactory().Build();
        var factory = new SholtoThemeFactory(
            new WaveformPaletteFactory(new TestWaveformPresets().Create(), stack.Defaults), new MinimapPaletteFactory(),
            stack.Defaults);
        var theme = factory.Create("""
            {
              "name": "No Load", "bgDeep": "#111111", "surface": "#1A1A1A", "surfaceRaised": "#222222",
              "border": "#333333", "primary": "#00FFCC", "accent": "#FFC700", "accentBg": "#33FFC700",
              "mint": "#FFFFFF", "textBright": "#EEEEEE", "textMuted": "#888888", "playedFadeColor": "#111111",
              "waveformPalette": "Bands",
              "camelotPalette": { "hueOffset": 0, "saturation": 0.78, "majorLightness": 0.55, "minorLightness": 0.42,
                                  "onChipForeground": "#101820" }
            }
            """);
        var host = new Border();
        new ThemeResourcesApplier().Apply(host.Resources, theme);
        Assert.True(stack.Defaults.TryGetColor("load.deck1", out var d1));
        Assert.True(stack.Defaults.TryGetColor("load.deck2", out var d2));
        Assert.True(stack.Defaults.TryGetColor("load.trackList", out var dl));
        Assert.Equal(d1, LoadColour(host, "SholtoLoadDeck1"));
        Assert.Equal(d2, LoadColour(host, "SholtoLoadDeck2"));
        Assert.Equal(dl, LoadColour(host, "SholtoLoadTrackList"));
    }

    [Fact]
    public void Apply_BirthdayMassacre_PublishesItsPinksAndViolet()
    {
        AvaloniaTestApp.EnsureStarted();
        var theme = new ThemeStackFactory().Build().Catalog.ByName("The Birthday Massacre");
        var host = new Border();
        new ThemeResourcesApplier().Apply(host.Resources, theme);
        Assert.Equal(Color.Parse("#FF3D9F"), LoadColour(host, "SholtoLoadDeck1"));
        Assert.Equal(Color.Parse("#FFA8D6"), LoadColour(host, "SholtoLoadDeck2"));
        Assert.Equal(Color.Parse("#A98BFF"), LoadColour(host, "SholtoLoadTrackList"));
    }

    [Theory]
    [InlineData("The Birthday Massacre")]
    [InlineData("Classic")]
    public void Apply_PublishesDeckWashesThatStartAboveTheSlotsSurface(string name)
    {
        AvaloniaTestApp.EnsureStarted();
        var theme = new ThemeStackFactory().Build().Catalog.ByName(name);
        var host = new Border();
        new ThemeResourcesApplier().Apply(host.Resources, theme);
        var raised = ((SolidColorBrush)theme.SurfaceRaised).Color;
        foreach (var key in new[] { "SholtoSlotDeck1Wash", "SholtoSlotDeck2Wash" })
        {
            var wash = Assert.IsType<LinearGradientBrush>(Resolve(host, key));
            Assert.NotEqual(raised, wash.GradientStops[0].Color);
        }
    }
}

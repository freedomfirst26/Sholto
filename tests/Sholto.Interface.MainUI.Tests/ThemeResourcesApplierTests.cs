using Avalonia.Controls;
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
        "SholtoSlotEmptyFill", "SholtoSlotHalo", "SholtoSlotSweep", "SholtoStatusAttention", "SholtoStatusError",
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
}

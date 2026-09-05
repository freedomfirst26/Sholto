using Avalonia.Media;
using Sholto.App.Theming;

namespace Sholto.App.Tests;

public class MinimapPaletteTests
{
    // Minimal but complete theme JSON — every field SholtoThemeJson.Parse requires,
    // with the "minimap" section supplied by the caller (or omitted).
    private static string ThemeJson(string? minimapSection) => $$"""
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
      "waveformPalette": "Bands",
      "camelotPalette": {
        "hueOffset": 0, "saturation": 0.8, "majorLightness": 0.55, "minorLightness": 0.42,
        "onChipForeground": "#101820"
      }
      {{(minimapSection is null ? "" : "," + minimapSection)}}
    }
    """;

    [Fact]
    public void ExplicitMinimapSection_YieldsExactColors()
    {
        var json = ThemeJson("""
            "minimap": {
              "backdrop":  "#010203",
              "playhead":  "#AABBCC",
              "label":     "#DDEEFF",
              "divider":   "#B0000000",
              "intro":     "#111111",
              "buildUp":   "#222222",
              "drop":      "#333333",
              "breakdown": "#444444",
              "verse":     "#555555",
              "chorus":    "#666666",
              "bridge":    "#777777",
              "outro":     "#888888"
            }
            """);

        var theme = SholtoThemeJson.Parse(json);
        var m = theme.Minimap;

        Assert.Equal(Color.Parse("#010203"), m.Backdrop);
        Assert.Equal(Color.Parse("#AABBCC"), m.Playhead);
        Assert.Equal(Color.Parse("#DDEEFF"), m.Label);
        Assert.Equal(Color.Parse("#B0000000"), m.Divider);
        Assert.Equal(Color.Parse("#111111"), m.Intro);
        Assert.Equal(Color.Parse("#222222"), m.BuildUp);
        Assert.Equal(Color.Parse("#333333"), m.Drop);
        Assert.Equal(Color.Parse("#444444"), m.Breakdown);
        Assert.Equal(Color.Parse("#555555"), m.Verse);
        Assert.Equal(Color.Parse("#666666"), m.Chorus);
        Assert.Equal(Color.Parse("#777777"), m.Bridge);
        Assert.Equal(Color.Parse("#888888"), m.Outro);
    }

    [Fact]
    public void MissingMinimapSection_YieldsDerivedPalette()
    {
        var theme = SholtoThemeJson.Parse(ThemeJson(minimapSection: null));
        var m = theme.Minimap;

        // Core anchors from the derivation rules.
        Assert.Equal(Color.Parse("#101010"), m.Backdrop); // bgDeep
        Assert.Equal(Color.Parse("#34F0C6"), m.Playhead); // mint
        Assert.Equal(Color.Parse("#FF4E9A"), m.Drop);     // accent
        Assert.Equal(Color.Parse("#EEEAFF"), m.Label);    // textBright
        Assert.Equal(Color.Parse("#34F0C6"), m.Bridge);   // mint
        Assert.Equal(Color.Parse("#7C5CFF"), m.BuildUp);  // primary
        Assert.Equal(Color.Parse("#404040"), m.Intro);    // border
    }

    [Fact]
    public void PartialMinimapSection_KeepsGivenKeyDerivesRest()
    {
        var explicitJson = ThemeJson("""
            "minimap": { "drop": "#FF0000" }
            """);
        var withPartial = SholtoThemeJson.Parse(explicitJson);
        var withNone = SholtoThemeJson.Parse(ThemeJson(minimapSection: null));

        // The explicit key wins...
        Assert.Equal(Color.Parse("#FF0000"), withPartial.Minimap.Drop);
        Assert.NotEqual(withNone.Minimap.Drop, withPartial.Minimap.Drop);

        // ...but everything else still matches the fully-derived palette.
        Assert.Equal(withNone.Minimap.Backdrop, withPartial.Minimap.Backdrop);
        Assert.Equal(withNone.Minimap.Playhead, withPartial.Minimap.Playhead);
        Assert.Equal(withNone.Minimap.Label, withPartial.Minimap.Label);
        Assert.Equal(withNone.Minimap.Divider, withPartial.Minimap.Divider);
        Assert.Equal(withNone.Minimap.Intro, withPartial.Minimap.Intro);
        Assert.Equal(withNone.Minimap.BuildUp, withPartial.Minimap.BuildUp);
        Assert.Equal(withNone.Minimap.Breakdown, withPartial.Minimap.Breakdown);
        Assert.Equal(withNone.Minimap.Verse, withPartial.Minimap.Verse);
        Assert.Equal(withNone.Minimap.Chorus, withPartial.Minimap.Chorus);
        Assert.Equal(withNone.Minimap.Bridge, withPartial.Minimap.Bridge);
        Assert.Equal(withNone.Minimap.Outro, withPartial.Minimap.Outro);
    }
}

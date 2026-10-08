using Avalonia.Media;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests.Minimap;

/// <summary>The bundled themes carry band-sourced section colours, so every section kind reads apart on the minimap
/// and a drop is the loudest thing on it.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public sealed class MinimapSectionColourTests
{
    private const double PairGap = 0.08;
    private const double DropGap = 0.10;
    private const double MinLightness = 0.45;
    private const double DropChromaLead = 0.05;

    private readonly OklabCodec _codec = new();

    public static TheoryData<string> Themes() => new()
    {
        "Classic", "Serato", "Front Line Assembly", "Silence Groove", "Jeremy Soule", "Type O Negative",
        "The Birthday Massacre", "Pantera", "Dimmu Borgir", "Aphex Twin", "The Prodigy",
    };

    public static TheoryData<string> MutedThemes() => new() { "Dimmu Borgir", "Jeremy Soule", "Type O Negative" };

    private Dictionary<string, (double L, double A, double B)> Kinds(string themeName)
    {
        AvaloniaTestApp.EnsureStarted();
        var m = new ThemeStackFactory().Build().Catalog.All.First(t => t.Name == themeName).Minimap;
        return new()
        {
            ["intro"] = _codec.Encode(m.Intro), ["buildUp"] = _codec.Encode(m.BuildUp),
            ["drop"] = _codec.Encode(m.Drop), ["breakdown"] = _codec.Encode(m.Breakdown),
            ["outro"] = _codec.Encode(m.Outro), ["chorus"] = _codec.Encode(m.Chorus),
            ["verse"] = _codec.Encode(m.Verse), ["bridge"] = _codec.Encode(m.Bridge),
        };
    }

    private void AssertPairsApart(Dictionary<string, (double L, double A, double B)> k, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
            for (int j = i + 1; j < names.Length; j++)
                Assert.True(_codec.Distance(k[names[i]], k[names[j]]) >= PairGap,
                    $"{names[i]} vs {names[j]} = {_codec.Distance(k[names[i]], k[names[j]]):F3}");
    }

    [Theory, MemberData(nameof(Themes))]
    public void ArrangementKinds_AreApart(string theme) =>
        AssertPairsApart(Kinds(theme), "intro", "buildUp", "drop", "breakdown", "outro", "chorus");

    [Theory, MemberData(nameof(Themes))]
    public void SongKinds_AreApart(string theme) =>
        AssertPairsApart(Kinds(theme), "intro", "verse", "chorus", "outro");

    [Theory, MemberData(nameof(Themes))]
    public void VerseChorusBridgeBuild_AreApart(string theme) =>
        AssertPairsApart(Kinds(theme), "verse", "chorus", "bridge", "buildUp");

    [Theory, MemberData(nameof(Themes))]
    public void Drop_IsFarFromEveryOtherKind(string theme)
    {
        var k = Kinds(theme);
        foreach (var other in k.Keys.Where(n => n != "drop"))
            Assert.True(_codec.Distance(k["drop"], k[other]) >= DropGap,
                $"drop vs {other} = {_codec.Distance(k["drop"], k[other]):F3}");
    }

    [Theory, MemberData(nameof(Themes))]
    public void EveryKind_IsLightEnoughToSee(string theme)
    {
        foreach (var (name, lab) in Kinds(theme))
            Assert.True(lab.L >= MinLightness, $"{name} L = {lab.L:F3}");
    }

    [Theory, MemberData(nameof(MutedThemes))]
    public void MutedThemes_DropLeadsInChroma(string theme)
    {
        var k = Kinds(theme);
        foreach (var other in k.Keys.Where(n => n != "drop"))
            Assert.True(_codec.Chroma(k["drop"]) - _codec.Chroma(k[other]) >= DropChromaLead,
                $"drop C {_codec.Chroma(k["drop"]):F3} vs {other} C {_codec.Chroma(k[other]):F3}");
    }
}

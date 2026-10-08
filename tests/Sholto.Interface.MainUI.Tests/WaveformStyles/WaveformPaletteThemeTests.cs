using Avalonia.Media;
using Sholto.Interface.MainUI.Tests.Minimap;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>The bundled themes carry the approved "married" waveform colours: the 3-BAND and RGB bands read apart,
/// and each palette is pinned to the approved values.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public sealed class WaveformPaletteThemeTests
{
    private const double Gap = 0.10;

    private readonly OklabCodec _codec = new();

    /// <summary>The approved palette per theme: 3-BAND low, mid, high, then RGB low, mid, high.</summary>
    private readonly Dictionary<string, (string Low, string Mid, string High, string RgbLow, string RgbMid, string RgbHigh)> _approved = new()
    {
        ["Classic"] = ("#2A7FFF", "#FF8C1A", "#F5F5FF", "#FF8C1A", "#00FFCC", "#3AA0FF"),
        ["Serato"] = ("#FF3D3D", "#3DFF7A", "#ABCCFF", "#FF3D3D", "#3DFF7A", "#3D8BFF"),
        ["Front Line Assembly"] = ("#F26B1D", "#FFB224", "#A8EBFA", "#F26B1D", "#FFB224", "#3FD4F0"),
        ["Silence Groove"] = ("#5BB8FF", "#FFC247", "#EAF4FF", "#8D7BFF", "#FFC247", "#35E6D0"),
        ["Jeremy Soule"] = ("#4A80A9", "#E0A030", "#BEE9F2", "#D9411E", "#E0A030", "#BEE9F2"),
        ["Type O Negative"] = ("#3DB54A", "#99E354", "#FFE3BD", "#3DB54A", "#C96736", "#D8A24F"),
        ["The Birthday Massacre"] = ("#943AE4", "#FF3D9F", "#E9DEFF", "#4569FA", "#FF3D9F", "#C9A8FF"),
        ["Pantera"] = ("#E01E1E", "#D9943E", "#F4F4F0", "#E01E1E", "#E49839", "#437FF7"),
        ["Dimmu Borgir"] = ("#CC272D", "#A8C1DA", "#EDEDE8", "#DF212D", "#C9A24A", "#A9C8E8"),
        ["Aphex Twin"] = ("#0097A7", "#C6FF1A", "#E7ECF2", "#FF5FD2", "#C6FF1A", "#3FB8C8"),
        ["The Prodigy"] = ("#27CE00", "#E8541E", "#FFEEB5", "#39FF14", "#E8541E", "#FF2A9A"),
    };

    public static TheoryData<string> Themes() => new()
    {
        "Classic", "Serato", "Front Line Assembly", "Silence Groove", "Jeremy Soule", "Type O Negative",
        "The Birthday Massacre", "Pantera", "Dimmu Borgir", "Aphex Twin", "The Prodigy",
    };

    private WaveformPalette Palette(string themeName)
    {
        AvaloniaTestApp.EnsureStarted();
        return new ThemeStackFactory().Build().Catalog.All.First(t => t.Name == themeName).Waveform;
    }

    /// <summary>Scales so the brightest channel is 255, which is what the RGB mixer draws.</summary>
    private Color Normalised(Color c)
    {
        double k = 255.0 / Math.Max(c.R, Math.Max(c.G, c.B));
        return Color.FromRgb((byte)Math.Round(c.R * k), (byte)Math.Round(c.G * k), (byte)Math.Round(c.B * k));
    }

    private void AssertApart(Color a, Color b, Color c)
    {
        var (x, y, z) = (_codec.Encode(a), _codec.Encode(b), _codec.Encode(c));
        Assert.True(_codec.Distance(x, y) >= Gap, $"first vs second = {_codec.Distance(x, y):F3}");
        Assert.True(_codec.Distance(x, z) >= Gap, $"first vs third = {_codec.Distance(x, z):F3}");
        Assert.True(_codec.Distance(y, z) >= Gap, $"second vs third = {_codec.Distance(y, z):F3}");
    }

    [Theory, MemberData(nameof(Themes))]
    public void ThreeBand_BandsAreApart(string theme)
    {
        var p = Palette(theme);
        AssertApart(p.Low, p.Mid, p.High);
    }

    [Theory, MemberData(nameof(Themes))]
    public void Rgb_BandsAreApart_AsTheMixerDrawsThem(string theme)
    {
        var p = Palette(theme);
        AssertApart(Normalised(p.RgbLow), Normalised(p.RgbMid), Normalised(p.RgbHigh));
    }

    [Theory, MemberData(nameof(Themes))]
    public void Palette_IsTheApprovedOne(string theme)
    {
        var p = Palette(theme);
        var a = _approved[theme];
        Assert.Equal(
            [Color.Parse(a.Low), Color.Parse(a.Mid), Color.Parse(a.High), Color.Parse(a.RgbLow), Color.Parse(a.RgbMid), Color.Parse(a.RgbHigh)],
            new[] { p.Low, p.Mid, p.High, p.RgbLow, p.RgbMid, p.RgbHigh });
    }
}

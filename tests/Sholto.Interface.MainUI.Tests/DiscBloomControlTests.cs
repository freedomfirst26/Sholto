using Avalonia.Media;
using Sholto.Interface.MainUI.Controls;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The disc bloom control keeps one set of brushes and retones them in place when the palette
/// changes.</summary>
public class DiscBloomControlTests
{
    private sealed class StubLevels(double low, double mid, double high) : IDiscBloomLevels
    {
        public double Low { get; } = low;
        public double Mid { get; } = mid;
        public double High { get; } = high;
        public event Action? Changed { add { } remove { } }
    }

    private readonly WaveformPalette _palette;

    public DiscBloomControlTests()
    {
        AvaloniaTestApp.EnsureStarted();
        _palette = new ThemeStackFactory().Build().Context.Current.Waveform;
    }

    [Fact]
    public void The_glows_take_the_three_band_colours_of_the_palette()
    {
        var control = new DiscBloomControl { Palette = _palette };

        Assert.Equal(Color.FromArgb(0xFF, _palette.Low.R, _palette.Low.G, _palette.Low.B), control.GlowColour(0));
        Assert.Equal(Color.FromArgb(0xFF, _palette.Mid.R, _palette.Mid.G, _palette.Mid.B), control.GlowColour(1));
        Assert.Equal(Color.FromArgb(0xFF, _palette.High.R, _palette.High.G, _palette.High.B), control.GlowColour(2));
    }

    [Fact]
    public void A_new_palette_retones_the_glows_immediately()
    {
        var control = new DiscBloomControl { Palette = _palette };

        control.Palette = _palette with { Low = Color.Parse("#112233"), Mid = Color.Parse("#445566"), High = Color.Parse("#778899") };

        Assert.Equal(Color.Parse("#112233"), control.GlowColour(0));
        Assert.Equal(Color.Parse("#445566"), control.GlowColour(1));
        Assert.Equal(Color.Parse("#778899"), control.GlowColour(2));
    }

    [Fact]
    public void The_levels_become_the_glow_opacities()
    {
        var control = new DiscBloomControl { Palette = _palette };

        control.ApplyLevels(new StubLevels(0.25, 0.5, 1.5));

        Assert.Equal(0.25, control.GlowOpacity(0));
        Assert.Equal(0.5, control.GlowOpacity(1));
        Assert.Equal(1.0, control.GlowOpacity(2));   // clamped
    }
}

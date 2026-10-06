using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using SkiaSharp;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class RgbWaveformStrategyTests
{
    private const int MidY = 128;

    private readonly IWaveformStyleStrategy _rgb = new WaveformStylesFactory().Create().ById("rgb");
    private readonly WaveformPalette _palette = new TestWaveformPalette().Create();
    private readonly BakedPixels _pixels = new();

    /// <summary>Eight columns: a loud kick and a loud hat with every band near its reference (a drop), a
    /// quieter kick and hat, a mid-led column, then silence. Each band's track reference is its own max.</summary>
    private static WaveformPeaks EightColumns()
    {
        //              kick   hat   kick  hat   mid   -    -    -
        float[] low  = [1.2f,  .8f,  1f,   .05f, .2f,  0f,  0f, 0f];
        float[] mid  = [.9f,   .9f,  .1f,  .1f,  1f,   0f,  0f, 0f];
        float[] high = [.8f,   1.1f, .1f,  1f,   .5f,  0f,  0f, 0f];
        float[] max  = [1f,    .5f,  .8f,  .6f,  .4f,  0f,  0f, 0f];
        float[] min  = [-.5f,  -.3f, -.3f, -.3f, -.4f, 0f,  0f, 0f];
        return new WaveformPeaks(min, max, low, mid, high, 1024, 48000);
    }

    [Fact]
    public void A_kick_column_is_red_and_a_hat_column_blue_even_with_every_band_loud()
    {
        using var image = _rgb.Bake(EightColumns(), _palette, CancellationToken.None)!;
        var loudKick = _pixels.At(image, 0, MidY);
        var loudHat = _pixels.At(image, 1, MidY);
        var kick = _pixels.At(image, 2, MidY);
        var hat = _pixels.At(image, 3, MidY);
        var midLed = _pixels.At(image, 4, MidY);

        Assert.True(loudKick.Red > loudKick.Blue && loudKick.Red > loudKick.Green, $"loud kick {loudKick}");
        Assert.True(loudHat.Blue > loudHat.Red && loudHat.Blue > loudHat.Green, $"loud hat {loudHat}");
        // Dimmed to this column's loudness (0.8 → ×0.92), so red is the top channel, not necessarily 255.
        Assert.True(kick.Red > 200 && kick.Green < 100 && kick.Blue < 100, $"kick {kick}");
        Assert.True(hat.Blue > hat.Red && hat.Blue >= hat.Green, $"hat {hat}");
        Assert.True(midLed.Green > midLed.Red && midLed.Green > midLed.Blue, $"mid-led {midLed}");
    }

    [Fact]
    public void Columns_are_solid_and_adjacent_and_silence_draws_nothing()
    {
        using var image = _rgb.Bake(EightColumns(), _palette, CancellationToken.None)!;
        var bg = _palette.Background.ToSk();
        var mask = _pixels.Coverage(image, bg);
        Assert.Equal((8, 256), (image.Width, image.Height));
        for (int x = 0; x < 5; x++) Assert.True(mask[MidY * image.Width + x], $"({x},{MidY}) should be lit");
        for (int x = 5; x < 8; x++)
            for (int y = 0; y < image.Height; y++)
                Assert.False(mask[y * image.Width + x], $"({x},{y}) should be background");
    }

    [Fact]
    public void No_gaps_inside_an_audible_run()
    {
        // Every column of the synthetic track is audible, so every column must be lit at the centre.
        using var image = _rgb.Bake(new TestWaveformPeaks().Create(), _palette, CancellationToken.None)!;
        var mask = _pixels.Coverage(image, _palette.Background.ToSk());
        for (int x = 0; x < image.Width; x++)
            Assert.True(mask[MidY * image.Width + x], $"column {x} is a gap");
    }

    [Fact]
    public void Column_height_follows_the_raw_peak_through_the_expansion_curve_mirrored_about_the_centre()
    {
        using var image = _rgb.Bake(EightColumns(), _palette, CancellationToken.None)!;
        var bg = _palette.Background.ToSk();
        // half = round(128 · 0.95 · (amp / loudest)²), loudest = 1.0.
        AssertHalfHeight(image, bg, x: 0, half: 122);   // 1.0  → 121.6
        AssertHalfHeight(image, bg, x: 1, half: 30);    // 0.5  → 30.4
        AssertHalfHeight(image, bg, x: 2, half: 78);    // 0.8  → 77.8
        AssertHalfHeight(image, bg, x: 3, half: 44);    // 0.6  → 43.8
        AssertHalfHeight(image, bg, x: 4, half: 19);    // |−.4| → 19.5
    }

    private void AssertHalfHeight(SKImage image, SKColor bg, int x, int half)
    {
        Assert.NotEqual(bg, _pixels.At(image, x, MidY - half));
        Assert.Equal(bg, _pixels.At(image, x, MidY - half - 1));
        Assert.NotEqual(bg, _pixels.At(image, x, MidY + half - 1));
        Assert.Equal(bg, _pixels.At(image, x, MidY + half));
    }

    [Fact]
    public void Brightness_follows_loudness()
    {
        using var image = _rgb.Bake(EightColumns(), _palette, CancellationToken.None)!;
        var loudest = _pixels.At(image, 0, MidY);
        var half = _pixels.At(image, 1, MidY);
        Assert.Equal(255, Brightest(loudest));
        // 0.35 + 0.65 · 0.5^0.6 ≈ 0.78 of full.
        Assert.InRange(Brightest(half), 190, 208);
        Assert.Equal(255, half.Alpha);
    }

    private int Brightest(SKColor c) => Math.Max(c.Red, Math.Max(c.Green, c.Blue));

    [Fact]
    public void A_kick_column_is_red_dominant_in_the_synthetic_track()
    {
        // Column 48 is a kick in TestWaveformPeaks (low 1, quiet highs).
        using var image = _rgb.Bake(new TestWaveformPeaks().Create(), _palette, CancellationToken.None)!;
        var c = _pixels.At(image, 48, MidY);
        Assert.True(c.Red > c.Green && c.Red > c.Blue, $"expected a red-led column, got {c}");
    }

    [Fact]
    public void Only_the_rgb_colours_and_background_force_a_rebake()
    {
        var p = _palette;
        Assert.True(_rgb.BakesSameColours(p, p with { Low = Avalonia.Media.Colors.Pink, Marker = Avalonia.Media.Colors.Pink }));
        Assert.False(_rgb.BakesSameColours(p, p with { RgbHigh = Avalonia.Media.Colors.Pink }));
        Assert.False(_rgb.BakesSameColours(p, p with { Background = Avalonia.Media.Colors.Pink }));
    }

    [Fact]
    public void Without_bands_it_still_bakes_one_colour()
    {
        var peaks = new TestWaveformPeaks().CreateWithoutBands();
        using var image = _rgb.Bake(peaks, _palette, CancellationToken.None)!;
        var even = new RgbColourMixer().Mix(1f, 1f, 1f, _palette.RgbLow.ToSk(), _palette.RgbMid.ToSk(), _palette.RgbHigh.ToSk());
        // Hue is the even mix; brightness follows this column's loudness, so compare channel ratios.
        var c = _pixels.At(image, 600, MidY);
        Assert.Equal(Brightest(even), 255);
        Assert.InRange(c.Red * 255.0 / Brightest(c), even.Red - 2, even.Red + 2);
        Assert.InRange(c.Green * 255.0 / Brightest(c), even.Green - 2, even.Green + 2);
        Assert.InRange(c.Blue * 255.0 / Brightest(c), even.Blue - 2, even.Blue + 2);
    }

    [Fact]
    public void A_cancelled_bake_returns_null_and_empty_peaks_bake_nothing()
    {
        Assert.Null(_rgb.Bake(EightColumns(), _palette, new CancellationToken(canceled: true)));
        Assert.Null(_rgb.Bake(new WaveformPeaks([], [], [], [], [], 1024, 48000), _palette, CancellationToken.None));
    }
}

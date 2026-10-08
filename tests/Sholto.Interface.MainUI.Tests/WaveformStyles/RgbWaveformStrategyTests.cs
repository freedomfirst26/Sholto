using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using SkiaSharp;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class RgbWaveformStrategyTests
{
    private const int MidY = 128;

    private readonly IWaveformStyleStrategy _rgb = new WaveformStylesFactory(Options.Create(new WaveformStyleOptions())).Create().ById("rgb");
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
    public void Sustained_kick_hat_and_mid_sections_tint_red_blue_and_green_even_with_every_band_loud()
    {
        // 48 columns each, every band near its reference (a drop). Colour is smoothed over neighbours, so
        // each section is judged well inside its own run.
        const int run = 48;
        float[][] bands = [[1f, .9f, .73f], [.67f, .9f, 1f], [.3f, 1f, .5f]];
        var low = new float[3 * run]; var mid = new float[3 * run]; var high = new float[3 * run];
        var max = new float[3 * run]; var min = new float[3 * run];
        for (int x = 0; x < 3 * run; x++)
        {
            var b = bands[x / run];
            (low[x], mid[x], high[x], max[x], min[x]) = (b[0], b[1], b[2], .8f, -.8f);
        }
        using var image = _rgb.Bake(new WaveformPeaks(min, max, low, mid, high, 1024, 48000), _palette, CancellationToken.None)!;
        var kick = _pixels.At(image, run / 2, MidY);
        var hat = _pixels.At(image, run + run / 2, MidY);
        var midLed = _pixels.At(image, 2 * run + run / 2, MidY);
        Assert.True(kick.Red > kick.Blue && kick.Red > kick.Green, $"kick {kick}");
        Assert.True(hat.Blue > hat.Red && hat.Blue > hat.Green, $"hat {hat}");
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
    public void Alternating_kick_and_hat_columns_blend_in_colour_but_heights_still_follow_each_column()
    {
        const int n = 64;
        var low = new float[n]; var mid = new float[n]; var high = new float[n];
        var max = new float[n]; var min = new float[n];
        for (int x = 0; x < n; x++)
        {
            bool kick = x % 2 == 0;
            low[x] = kick ? 1f : .1f; mid[x] = .5f; high[x] = kick ? .1f : 1f;
            max[x] = kick ? .9f : .4f; min[x] = -max[x];
        }
        using var image = _rgb.Bake(new WaveformPeaks(min, max, low, mid, high, 1024, 48000), _palette, CancellationToken.None)!;
        for (int x = 20; x < 43; x++)
        {
            _pixels.At(image, x, MidY).ToHsv(out float h1, out _, out _);
            _pixels.At(image, x + 1, MidY).ToHsv(out float h2, out _, out _);
            float d = MathF.Abs(h1 - h2); d = MathF.Min(d, 360f - d);
            Assert.True(d < 12f, $"hue jumps {d} degrees between columns {x} and {x + 1}");
        }
        var bg = _palette.Background.ToSk();
        Assert.NotEqual(bg, _pixels.At(image, 30, MidY - 100));   // kick column is tall
        Assert.Equal(bg, _pixels.At(image, 31, MidY - 100));      // hat column is short
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
        var even = new RgbColourMixer(0.75f).Mix(1f, 1f, 1f, _palette.RgbLow.ToSk(), _palette.RgbMid.ToSk(), _palette.RgbHigh.ToSk());
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

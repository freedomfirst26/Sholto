using Sholto.Interface.MainUI.Controls.WaveformStyles;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>3-BAND must draw exactly what WaveformControl drew before the style strategies existed.
/// The hashes were recorded from the pre-refactor <c>WaveformControl.BakeWaveform</c> (called by
/// reflection) on the same synthetic peaks and palette, at f0dea0f. If Skia itself is upgraded these can
/// change without a regression; re-record them from the parent commit's bake in that case.</summary>
public class ThreeBandWaveformStrategyTests
{
    private const string WithBandsSha = "4BF1DA58C45A872468992D20BC17874D59A5006AC3D7A7D4D2A3C3AB34051B98";
    private const string WithoutBandsSha = "468C9D25EC6E71B6EF9E5B83F56EEEA40AE4F5785595854693D809D4DE1C8824";

    private readonly IWaveformStyleStrategy _threeBand = new WaveformStylesFactory().Create().ById("three-band");

    [Fact]
    public void Bake_with_bands_is_pixel_identical_to_the_pre_strategy_bake()
    {
        using var image = _threeBand.Bake(new TestWaveformPeaks().Create(), new TestWaveformPalette().Create(), CancellationToken.None)!;
        Assert.Equal((1200, 256), (image.Width, image.Height));
        Assert.Equal(WithBandsSha, new BakedPixels().Sha256(image));
    }

    [Fact]
    public void Bake_without_bands_is_pixel_identical_to_the_pre_strategy_bake()
    {
        using var image = _threeBand.Bake(new TestWaveformPeaks().CreateWithoutBands(), new TestWaveformPalette().Create(), CancellationToken.None)!;
        Assert.Equal(WithoutBandsSha, new BakedPixels().Sha256(image));
    }

    [Fact]
    public void A_cancelled_bake_returns_null()
    {
        var cancelled = new CancellationToken(canceled: true);
        Assert.Null(_threeBand.Bake(new TestWaveformPeaks().Create(), new TestWaveformPalette().Create(), cancelled));
    }

    [Fact]
    public void Only_the_three_band_colours_and_background_force_a_rebake()
    {
        var p = new TestWaveformPalette().Create();
        Assert.True(_threeBand.BakesSameColours(p, p with { Marker = Avalonia.Media.Colors.Pink, RgbLow = Avalonia.Media.Colors.Pink }));
        Assert.False(_threeBand.BakesSameColours(p, p with { Mid = Avalonia.Media.Colors.Pink }));
        Assert.False(_threeBand.BakesSameColours(p, p with { Background = Avalonia.Media.Colors.Pink }));
    }
}

using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Interface.MainUI.Controls.Minimap;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests.Minimap;

public class MinimapPeakDownsamplerTests
{
    private readonly MinimapPeakDownsampler _down = new();

    private WaveformPeaks Ramp(int n, bool bands = true)
    {
        var min = new float[n]; var max = new float[n]; var low = new float[n]; var mid = new float[n]; var high = new float[n];
        for (int i = 0; i < n; i++) { max[i] = i / (float)n; min[i] = -max[i]; low[i] = i; mid[i] = 2 * i; high[i] = 0; }
        return bands ? new WaveformPeaks(min, max, low, mid, high, 512, 48000) : new WaveformPeaks(min, max, [], [], [], 512, 48000);
    }

    [Fact]
    public void A_single_spike_survives_in_Min_and_Max()
    {
        var p = Ramp(1000);
        p.Max[437] = 0.99f; p.Min[437] = -0.97f;

        var d = _down.Downsample(p, 100);

        Assert.Equal(100, d.Min.Length);
        Assert.Equal(0.99f, d.Max[43]);   // 437 lies in columns 430..439 -> output 43
        Assert.Equal(-0.97f, d.Min[43]);
    }

    [Fact]
    public void Output_columns_cover_the_source_exactly_once_at_both_edges()
    {
        var p = Ramp(1003);
        for (int i = 0; i < 1003; i++) { p.Max[i] = 0.1f; p.Min[i] = -0.1f; }
        p.Max[0] = 0.8f; p.Max[1002] = 0.9f; p.Min[1002] = -0.95f;

        var d = _down.Downsample(p, 100);

        Assert.Equal(0.8f, d.Max[0]);
        Assert.Equal(0.9f, d.Max[99]);
        Assert.Equal(0.1f, d.Max[98]);   // 1002 is not in column 98 ([983, 993))
        Assert.Equal(-0.95f, d.Min[99]);
    }

    [Fact]
    public void Bands_are_the_mean_and_max_of_the_range_half_each()
    {
        var p = Ramp(100);   // Low[i] = i; output 0 covers 0..9: mean 4.5, max 9
        var d = _down.Downsample(p, 10);

        Assert.Equal((4.5f + 9f) / 2f, d.Low[0], 4);
        Assert.Equal((9f + 18f) / 2f, d.Mid[0], 4);
    }

    [Fact]
    public void More_columns_than_peaks_repeats_the_source_and_reads_in_range()
    {
        var p = Ramp(10);
        var d = _down.Downsample(p, 25);

        Assert.Equal(25, d.Max.Length);
        Assert.Equal(p.Max[0], d.Max[0]);
        Assert.Equal(p.Max[9], d.Max[24]);
    }

    [Fact]
    public void No_band_data_gives_no_band_arrays()
    {
        var d = _down.Downsample(Ramp(100, bands: false), 10);

        Assert.Empty(d.Low); Assert.Empty(d.Mid); Assert.Empty(d.High);
        Assert.Equal(10, d.Min.Length);
    }

    [Fact]
    public void Empty_input_or_no_columns_gives_empty_peaks()
    {
        Assert.Empty(_down.Downsample(Ramp(100), 0).Min);
        Assert.Empty(_down.Downsample(new WaveformPeaks([], [], [], [], [], 512, 48000), 50).Min);
    }
}

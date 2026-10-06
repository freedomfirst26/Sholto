using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Interface.MainUI.Controls.WaveformStyles;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class DemoWaveformFactoryTests
{
    private const double PeaksPerBeat = 60.0 / 128 * 48000 / WaveformDefaults.SamplesPerPeak;

    private static float Average(float[] band, int fromBeat, int toBeat)
    {
        int a = (int)(fromBeat * PeaksPerBeat), b = (int)(toBeat * PeaksPerBeat);
        float sum = 0;
        for (int i = a; i < b; i++) sum += band[i];
        return sum / (b - a);
    }

    [Fact]
    public void Is_the_same_instance_every_call_and_the_same_peaks_from_a_second_factory()
    {
        var first = new DemoWaveformFactory();
        Assert.Same(first.Peaks, first.Peaks);

        var again = new DemoWaveformFactory().Peaks;
        Assert.Equal(first.Peaks.Low, again.Low);
        Assert.Equal(first.Peaks.Mid, again.Mid);
        Assert.Equal(first.Peaks.High, again.High);
        Assert.Equal(first.Peaks.Max, again.Max);
        Assert.Equal(first.Peaks.Min, again.Min);
    }

    [Fact]
    public void Has_the_analyzer_shape_and_about_two_minutes_at_the_deck_resolution()
    {
        var peaks = new DemoWaveformFactory().Peaks;
        Assert.Equal(WaveformDefaults.SamplesPerPeak, peaks.SamplesPerPeak);
        int n = peaks.Min.Length;
        Assert.Equal(n, peaks.Max.Length);
        Assert.Equal(n, peaks.Low.Length);
        Assert.Equal(n, peaks.Mid.Length);
        Assert.Equal(n, peaks.High.Length);
        double seconds = n * (double)peaks.SamplesPerPeak / 48000;
        Assert.InRange(seconds, 118, 122);
        Assert.All(peaks.Low, v => Assert.InRange(v, 0f, 1f));
        Assert.All(peaks.Max, v => Assert.InRange(v, 0f, 1f));
    }

    [Fact]
    public void Sections_differ_in_level()
    {
        var p = new DemoWaveformFactory().Peaks;
        float breakdownLow = Average(p.Low, 100, 124);
        float dropLow = Average(p.Low, 140, 196);
        float grooveLow = Average(p.Low, 20, 60);
        Assert.True(breakdownLow < dropLow * 0.4f, $"breakdown {breakdownLow} vs drop {dropLow}");
        Assert.True(dropLow > grooveLow, $"drop {dropLow} vs groove {grooveLow}");
        // The breakdown is mids, not nothing.
        Assert.True(Average(p.Mid, 100, 124) > Average(p.Mid, 20, 24) * 0.8f);
        // Short gap at the end of the snare roll.
        Assert.True(Average(p.Mid, 135, 136) < Average(p.Mid, 130, 134) * 0.3f);
    }
}

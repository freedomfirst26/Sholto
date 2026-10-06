using Microsoft.Extensions.Options;
using Xunit;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Audio.Tests;

public class WaveformPeaksTests
{
    // Built the way AnalysisStackFactory composes it.
    private readonly IWaveformPeakAnalyzer _analyzer = new WaveformPeakAnalyzer(
        new WaveformBandSplitterFactory(new BiquadFactory(), Options.Create(new WaveformBandOptions())),
        new WaveformPeaksFactory());

    [Fact]
    public void Compute_EmptySamples_ReturnsEmpty()
    {
        var peaks = _analyzer.Compute([], channels: 2, sampleRate: AudioFileDecoder.TargetSampleRate);
        Assert.Empty(peaks.Min);
        Assert.Empty(peaks.Max);
    }

    [Fact]
    public void Compute_SinglePeak_CapturesMinAndMax()
    {
        // 512 stereo frames = 1024 samples; left channel = 0.5f, right = 0
        float[] samples = new float[1024];
        for (int i = 0; i < 1024; i += 2) samples[i] = 0.5f;

        var peaks = _analyzer.Compute(samples, channels: 2, sampleRate: AudioFileDecoder.TargetSampleRate, samplesPerPeak: 512);

        Assert.Single(peaks.Min);
        Assert.Single(peaks.Max);
        Assert.Equal(0f, peaks.Min[0], precision: 4);
        Assert.Equal(0.5f, peaks.Max[0], precision: 4);
    }

    [Fact]
    public void Compute_MultiplePeaks_CorrectCount()
    {
        float[] samples = new float[4096]; // 2048 stereo frames
        var peaks = _analyzer.Compute(samples, channels: 2, sampleRate: AudioFileDecoder.TargetSampleRate, samplesPerPeak: 512);
        Assert.Equal(4, peaks.Min.Length); // 2048 / 512 = 4 peaks
    }

    [Fact]
    public void Compute_PositiveAndNegative_CapturesBothSides()
    {
        float[] samples = new float[1024];
        samples[0] = 0.8f;   // left channel frame 0, positive
        samples[2] = -0.6f;  // left channel frame 1, negative

        var peaks = _analyzer.Compute(samples, channels: 2, sampleRate: AudioFileDecoder.TargetSampleRate, samplesPerPeak: 512);

        Assert.Equal(-0.6f, peaks.Min[0], precision: 4);
        Assert.Equal(0.8f, peaks.Max[0], precision: 4);
    }

    private WaveformPeaks Tone(double hz)
    {
        const int sr = 48000;
        var samples = new float[sr * 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = 0.8f * (float)Math.Sin(2 * Math.PI * hz * i / sr);
        return _analyzer.Compute(samples, channels: 1, sampleRate: sr, normalizeBands: false);
    }

    private static (float Low, float Mid, float High) Settled(WaveformPeaks p)
    {
        int i = p.Low.Length / 2; // past filter warm-up
        return (p.Low[i], p.Mid[i], p.High[i]);
    }

    [Fact]
    public void Compute_60HzTone_LandsInLow()
    {
        var (lo, mid, hi) = Settled(Tone(60));
        Assert.True(lo > 2 * mid, $"low={lo} mid={mid}");
        Assert.True(lo > 5 * hi, $"low={lo} high={hi}");
    }

    [Fact]
    public void Compute_1kHzTone_LandsMostlyInMid()
    {
        var (lo, mid, hi) = Settled(Tone(1000));
        Assert.True(mid > 3 * lo, $"mid={mid} low={lo}");
        Assert.True(mid > 3 * hi, $"mid={mid} high={hi}");
    }

    [Fact]
    public void Compute_10kHzTone_LandsInHigh()
    {
        var (lo, mid, hi) = Settled(Tone(10000));
        Assert.True(hi > 5 * lo, $"high={hi} low={lo}");
        Assert.True(hi > 1.5f * mid, $"high={hi} mid={mid}");
    }
}

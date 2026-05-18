using Xunit;
using CommunityDj.Analysis;

namespace CommunityDj.Audio.Tests;

public class WaveformPeaksTests
{
    [Fact]
    public void Compute_EmptySamples_ReturnsEmpty()
    {
        var peaks = WaveformPeaks.Compute([], channels: 2);
        Assert.Empty(peaks.Min);
        Assert.Empty(peaks.Max);
    }

    [Fact]
    public void Compute_SinglePeak_CapturesMinAndMax()
    {
        // 512 stereo frames = 1024 samples; left channel = 0.5f, right = 0
        float[] samples = new float[1024];
        for (int i = 0; i < 1024; i += 2) samples[i] = 0.5f;

        var peaks = WaveformPeaks.Compute(samples, channels: 2, samplesPerPeak: 512);

        Assert.Single(peaks.Min);
        Assert.Single(peaks.Max);
        Assert.Equal(0f, peaks.Min[0], precision: 4);
        Assert.Equal(0.5f, peaks.Max[0], precision: 4);
    }

    [Fact]
    public void Compute_MultiplePeaks_CorrectCount()
    {
        float[] samples = new float[4096]; // 2048 stereo frames
        var peaks = WaveformPeaks.Compute(samples, channels: 2, samplesPerPeak: 512);
        Assert.Equal(4, peaks.Min.Length); // 2048 / 512 = 4 peaks
    }

    [Fact]
    public void Compute_PositiveAndNegative_CapturesBothSides()
    {
        float[] samples = new float[1024];
        samples[0] = 0.8f;   // left channel frame 0, positive
        samples[2] = -0.6f;  // left channel frame 1, negative

        var peaks = WaveformPeaks.Compute(samples, channels: 2, samplesPerPeak: 512);

        Assert.Equal(-0.6f, peaks.Min[0], precision: 4);
        Assert.Equal(0.8f, peaks.Max[0], precision: 4);
    }
}

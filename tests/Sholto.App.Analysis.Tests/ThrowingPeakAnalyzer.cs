using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Analysis.Tests;

/// <summary>Peak analyzer that always throws, standing in for a waveform computation that fails.</summary>
internal sealed class ThrowingPeakAnalyzer(string message) : IWaveformPeakAnalyzer
{
    private readonly string _message = message;

    public WaveformPeaks Compute(
        float[] samples, int channels, int sampleRate,
        int samplesPerPeak = WaveformDefaults.SamplesPerPeak, bool normalizeBands = true) =>
        throw new InvalidOperationException(_message);
}

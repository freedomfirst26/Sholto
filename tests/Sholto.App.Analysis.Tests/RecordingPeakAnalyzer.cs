using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Analysis.Tests;

/// <summary>Peak analyzer that, when it runs, waits (bounded) for <paramref name="otherWorkStarted"/> and records
/// whether that other work had started by then, delegating the compute to the real analyzer.</summary>
internal sealed class RecordingPeakAnalyzer(Func<bool> otherWorkStarted) : IWaveformPeakAnalyzer
{
    private readonly Func<bool> _otherWorkStarted = otherWorkStarted;
    private readonly WaveformPeakAnalyzer _real = new(
        new WaveformBandSplitterFactory(new BiquadFactory(), Options.Create(new WaveformBandOptions())),
        new WaveformPeaksFactory());
    public bool OtherWorkRanAlongside { get; private set; }

    public WaveformPeaks Compute(
        float[] samples, int channels, int sampleRate,
        int samplesPerPeak = WaveformDefaults.SamplesPerPeak, bool normalizeBands = true)
    {
        OtherWorkRanAlongside = SpinWait.SpinUntil(_otherWorkStarted, 3000);
        return _real.Compute(samples, channels, sampleRate, samplesPerPeak, normalizeBands);
    }
}

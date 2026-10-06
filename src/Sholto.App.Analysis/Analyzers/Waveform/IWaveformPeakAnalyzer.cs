using Sholto.App.Analysis.Analyzers;
using Sholto.Data;

namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>
/// Computes <see cref="WaveformPeaks"/> from decoded PCM. Constructor-injected
/// into <c>Deck</c> (via <c>DeckFactory</c>) and into <c>BasicAnalysisStage.RunAsync</c>'s
/// caller so a test/bench harness can substitute a fast fake — the real
/// implementation is ~100-200 ms per 4-minute track (the stem stage computes vocal peaks once per stem load).
/// </summary>
public interface IWaveformPeakAnalyzer : IAnalyzer
{
    /// <summary>
    /// Computes min/max + per-band peak amplitudes from interleaved float samples.
    /// Set <paramref name="normalizeBands"/> to false when computing peaks that will
    /// later be merged with other peaks (e.g. one per stem) — independent per-stem
    /// normalization breaks cross-stem comparison; normalize the merged result instead.
    /// </summary>
    WaveformPeaks Compute(
        float[] samples, int channels, int sampleRate,
        int samplesPerPeak = WaveformDefaults.SamplesPerPeak, bool normalizeBands = true);
}

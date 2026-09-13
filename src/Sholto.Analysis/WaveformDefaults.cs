namespace Sholto.Analysis;

/// <summary>
/// Default parameter values shared between each waveform port and its default
/// implementation. C# bakes a default parameter value into the CALL SITE based
/// on the static type used there, so a caller holding <see cref="IWaveformPeakAnalyzer"/>
/// (or <see cref="IWaveformBandScaler"/>) and one holding the concrete class would
/// silently get different defaults if the two declarations drifted — with no
/// compiler warning. Both sides reference these consts instead of repeating the
/// literal.
/// </summary>
public static class WaveformDefaults
{
    /// <summary>NOTE: this is stale for this app (everything actually runs at
    /// <c>AudioFileDecoder.TargetSampleRate</c> = 48000) but is only harmless
    /// today because every real caller (see <c>DeckViewModel.cs</c>) passes the
    /// sample rate explicitly. Left unchanged — fixing it is a behaviour change,
    /// not a dedup.</summary>
    public const int SampleRate = 44100;

    public const int SamplesPerPeak = 1024;

    public const float Percentile = 0.97f;
}

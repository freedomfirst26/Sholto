namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>
/// Default parameter values shared between each waveform port and its default
/// implementation. C# bakes a default parameter value into the CALL SITE based
/// on the static type used there, so a caller holding <see cref="IWaveformPeakAnalyzer"/>
/// and one holding the concrete class would
/// silently get different defaults if the two declarations drifted — with no
/// compiler warning. Both sides reference these consts instead of repeating the
/// literal.
/// </summary>
public static class WaveformDefaults
{
    public const int SamplesPerPeak = 1024;
}

namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>Owns the named <see cref="WaveformPeaks"/> recipes, so no call site spells
/// out what "no peaks" is. Built at the composition root and injected; holds no
/// mutable state.</summary>
public sealed class WaveformPeaksFactory : IWaveformPeaksFactory
{
    private readonly WaveformPeaks _none = new([], [], [], [], [], 512);

    /// <summary>Peaks for a track with no samples. The same instance every call.</summary>
    public WaveformPeaks None() => _none;
}

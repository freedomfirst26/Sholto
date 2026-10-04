namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>Filter settings for the waveform band split, supplied via the standard
/// <c>IOptions&lt;WaveformBandOptions&gt;</c> pipeline. Affects the waveform's band
/// colours only.</summary>
public sealed class WaveformBandOptions
{
    /// <summary>Resonance (Q) of the low-pass filter that isolates the low band.
    /// 0.707 is Butterworth: maximally flat, no peaking at the cutoff.</summary>
    public float LowPassQ { get; set; } = 0.707f;

    /// <summary>Resonance (Q) of the high-pass filter that isolates the high band.
    /// 0.707 is Butterworth: maximally flat, no peaking at the cutoff.</summary>
    public float HighPassQ { get; set; } = 0.707f;
}

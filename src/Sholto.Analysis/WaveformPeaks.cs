namespace Sholto.Analysis;

/// <summary>
/// Pre-computed waveform peaks for rendering. Pure visual data — one column per peak.
/// Min/Max give the outline; Low/Mid/High give per-band energy [0..1] for color rendering.
/// </summary>
public sealed record WaveformPeaks(
    float[] Min,
    float[] Max,
    float[] Low,
    float[] Mid,
    float[] High,
    int SamplesPerPeak)
{
    public static WaveformPeaks Empty { get; } = new([], [], [], [], [], 512);
}

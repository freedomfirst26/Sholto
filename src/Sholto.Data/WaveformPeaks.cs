namespace Sholto.Data;

/// <summary>
/// Pre-computed waveform peaks for rendering. Pure visual data — one column per peak.
/// Min/Max give the outline; Low/Mid/High give per-band energy [0..1] for color rendering.
/// <see cref="SampleRate"/> is the rate the columns were measured at, so a column's time is
/// <see cref="SecondsPerPeak"/> and no reader needs to know the engine rate.
/// </summary>
public sealed record WaveformPeaks(
    float[] Min,
    float[] Max,
    float[] Low,
    float[] Mid,
    float[] High,
    int SamplesPerPeak,
    int SampleRate)
{
    /// <summary>Seconds of audio one column covers.</summary>
    public double SecondsPerPeak => SamplesPerPeak / (double)SampleRate;
}

namespace Sholto.Analysis;

/// <summary>
/// The low/mid/high crossover points shared by the waveform's 3-band colouring
/// (<see cref="WaveformPeakAnalyzer"/>) and the isolator EQ's crossover filters
/// (<c>Sholto.Audio.BiquadEq3Band</c>). Defined here — not independently in each
/// project — so the waveform can never drift from what the LOW/MID/HIGH knobs
/// actually cut: <c>Sholto.Audio</c> references <c>Sholto.Analysis</c> (not the
/// other way around), so this is the legal direction for a shared value without
/// giving <c>Sholto.Analysis</c> a ProjectReference of its own.
/// </summary>
public static class WaveformBandFrequencies
{
    /// <summary>Low/mid crossover, in Hz.</summary>
    public const float LowMidHz = 250f;

    /// <summary>Mid/high crossover, in Hz.</summary>
    public const float MidHighHz = 4000f;
}

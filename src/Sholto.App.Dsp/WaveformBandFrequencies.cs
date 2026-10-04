namespace Sholto.App.Dsp;

/// <summary>
/// The low/mid/high crossover points shared by the waveform's 3-band colouring
/// (<c>Sholto.App.Analysis.WaveformPeakAnalyzer</c>) and the isolator EQ's crossover
/// filters (<c>Sholto.App.Audio.BiquadEq3Band</c>). Defined here, in the shared
/// dependency-free DSP leaf, so the waveform can never drift from what the
/// LOW/MID/HIGH knobs actually cut — neither Sholto.App.Analysis nor Sholto.App.Audio
/// has to reach into the other to share it.
/// </summary>
public static class WaveformBandFrequencies
{
    /// <summary>Low/mid crossover, in Hz.</summary>
    public const float LowMidHz = 250f;

    /// <summary>Mid/high crossover, in Hz.</summary>
    public const float MidHighHz = 4000f;
}

namespace Sholto.Analysis;

/// <summary>
/// Per-stem waveform peaks. Computed from each of the 4 decoded stem buffers
/// after stems become available, so the deck's waveform can recompose itself
/// (by merging across currently-active stems) as the user toggles DRMS / VOX /
/// INST on and off.
///
/// Each <see cref="WaveformPeaks"/> here has the same SamplesPerPeak as the
/// others — they were computed from buffers of identical length, in lockstep —
/// so they can be merged slot-for-slot at render time.
/// </summary>
public sealed record StemPeaks(
    WaveformPeaks Drums,
    WaveformPeaks Vocals,
    WaveformPeaks Bass,
    WaveformPeaks Other) : IAnalysis
{
    public string Name => "StemPeaks";
}

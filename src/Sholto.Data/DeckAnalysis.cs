namespace Sholto.Data;

/// <summary>What analysis has found so far for the loaded track, as a deck shows it. An immutable snapshot:
/// steps that have not landed yet are empty, and each step that lands publishes a new one in
/// <see cref="DeckContentChanged"/>.</summary>
/// <param name="Peaks">Mixed band peaks, or null until basic analysis lands.</param>
/// <param name="Bpm">Source tempo; 0 until basic analysis lands.</param>
/// <param name="BeatTimes">Beat positions in seconds; empty until basic analysis lands.</param>
/// <param name="DownbeatTimes">Downbeat positions in seconds; empty until basic analysis lands.</param>
/// <param name="VocalRegions">Vocal-presence regions, or null until stems land.</param>
/// <param name="HasStems">Stems have landed.</param>
/// <param name="Key">The analysed key, or null until key analysis completes.</param>
public sealed record DeckAnalysis(
    WaveformPeaks? Peaks, double Bpm, double[] BeatTimes, double[] DownbeatTimes,
    IReadOnlyList<VocalRegion>? VocalRegions, bool HasStems, KeyRef? Key)
{
    /// <summary>Basic analysis (peaks, tempo, grid) has landed.</summary>
    public bool HasBasic => Peaks is not null;
}

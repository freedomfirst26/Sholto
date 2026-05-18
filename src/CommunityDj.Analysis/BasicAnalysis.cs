namespace CommunityDj.Analysis;

/// <summary>
/// First-pass track analysis: waveform peaks (with per-band amplitudes), BPM,
/// beat timestamps, and real downbeats. Everything needed to render the track
/// + beat-grid in one record.
/// </summary>
public sealed record BasicAnalysis(
    WaveformPeaks Peaks,
    double Bpm,
    double[] BeatTimes,
    double[] DownbeatTimes) : IAnalysis
{
    public string Name => "Basic";

    public static BasicAnalysis Empty { get; } = new(WaveformPeaks.Empty, 0.0, [], []);

    /// <summary>
    /// Build waveform peaks from the decoded samples and run madmom on the file
    /// for BPM + beats + downbeats.
    /// </summary>
    public static async Task<BasicAnalysis> ComputeAsync(
        string filePath,
        float[] stereoSamples,
        int channels,
        int sampleRate,
        CancellationToken ct = default)
    {
        var (peaks, _, _) = WaveformPeaks.ComputeWithOnsets(stereoSamples, channels, sampleRate);
        var (bpm, beats, downbeats) = await MadmomBeatAnalyzer.AnalyzeAsync(filePath, ct);
        return new BasicAnalysis(peaks, bpm, beats, downbeats);
    }
}

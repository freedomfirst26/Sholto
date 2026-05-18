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
    public const string WaveformStep = "waveform";
    public const string BeatsStep    = "beats";

    public string Name => "Basic";

    public static BasicAnalysis Empty { get; } = new(WaveformPeaks.Empty, 0.0, [], []);

    /// <summary>
    /// Build waveform peaks from the decoded samples and run madmom on the file
    /// for BPM + beats + downbeats. Reports progress through <paramref name="reporter"/>
    /// if supplied.
    /// </summary>
    public static async Task<BasicAnalysis> ComputeAsync(
        string filePath,
        float[] stereoSamples,
        int channels,
        int sampleRate,
        AnalysisReporter? reporter = null,
        CancellationToken ct = default)
    {
        reporter?.Running(filePath, WaveformStep);
        var (peaks, _, _) = WaveformPeaks.ComputeWithOnsets(stereoSamples, channels, sampleRate);
        reporter?.Complete(filePath, WaveformStep);

        reporter?.Running(filePath, BeatsStep);
        try
        {
            var (bpm, beats, downbeats) = await MadmomBeatAnalyzer.AnalyzeAsync(filePath, ct);
            reporter?.Complete(filePath, BeatsStep, $"{bpm:F1} BPM, {downbeats.Length} downbeats");
            return new BasicAnalysis(peaks, bpm, beats, downbeats);
        }
        catch (Exception ex)
        {
            reporter?.Failed(filePath, BeatsStep, ex.Message);
            throw;
        }
    }
}

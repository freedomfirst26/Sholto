namespace OpenDJ.Audio.Analysis;

/// <summary>
/// First-pass track analysis: waveform peaks (with per-band amplitudes), BPM,
/// beat timestamps, and (when available) real downbeats. This is what's needed
/// to render the track and beat-grid.
/// </summary>
public sealed record BasicAnalysis(
    WaveformPeaks Peaks,
    double Bpm,
    double[] BeatTimes,
    double[]? DownbeatTimes,
    string AnalyzerName) : IAnalysis
{
    public string Name => "Basic";

    public static BasicAnalysis Empty { get; } = new(WaveformPeaks.Empty, 0.0, [], null, "");

    /// <summary>
    /// Build waveform peaks from the decoded samples (fast, in-process), then
    /// run the highest-quality available beat analyzer (madmom if installed,
    /// otherwise the in-process Ellis fallback).
    /// </summary>
    public static async Task<BasicAnalysis> ComputeAsync(
        string filePath,
        float[] stereoSamples,
        int channels,
        int sampleRate,
        CancellationToken ct = default)
    {
        var (peaks, _, _) = WaveformPeaks.ComputeWithOnsets(stereoSamples, channels, sampleRate);

        IBeatAnalyzer analyzer = new MadmomBeatAnalyzer();
        if (!analyzer.IsAvailable) analyzer = new EllisBeatAnalyzer();

        BeatResult beats;
        try
        {
            beats = await analyzer.AnalyzeAsync(filePath, stereoSamples, sampleRate, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BasicAnalysis] {analyzer.GetType().Name} failed: {ex.Message} — falling back to Ellis");
            beats = await new EllisBeatAnalyzer().AnalyzeAsync(filePath, stereoSamples, sampleRate, ct);
            analyzer = new EllisBeatAnalyzer();
        }

        return new BasicAnalysis(peaks, beats.Bpm, beats.BeatTimes, beats.DownbeatTimes, analyzer.GetType().Name);
    }
}

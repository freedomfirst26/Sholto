namespace Sholto.Analysis;

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
    /// Build waveform peaks from the decoded samples and run the beat analysis
    /// step for BPM + beats + downbeats. Reports progress through <paramref name="reporter"/>
    /// if supplied.
    /// </summary>
    public static async Task<BasicAnalysis> ComputeAsync(
        string filePath,
        float[] stereoSamples,
        int channels,
        int sampleRate,
        IBeatAnalysisStep beatAnalyzer,
        IWaveformPeakAnalyzer peakAnalyzer,
        IBeatgridFitter beatgridFitter,
        IAnalysisReporter reporter,
        CancellationToken ct = default)
    {
        reporter.Running(filePath, AnalysisSteps.Waveform);
        var peaks = peakAnalyzer.Compute(stereoSamples, channels, sampleRate);
        reporter.Complete(filePath, AnalysisSteps.Waveform);

        reporter.Running(filePath, AnalysisSteps.Beats);
        try
        {
            var (bpm, rawBeats, rawDownbeats) = await beatAnalyzer.AnalyzeAsync(filePath, ct);

            // Replace the beat analysis step's raw beat + downbeat detections
            // with a constant-spacing beatgrid derived from the song's BPM and
            // the densest-cluster phase anchor. Every Nth synthesized beat is a
            // synthesized downbeat by construction — guarantees the waveform's
            // small beat ticks always coincide with the tall downbeat bars, and
            // gives sync / quantised-loops a single canonical grid.
            double durationSec = stereoSamples.Length / (double)Math.Max(channels, 1) / Math.Max(sampleRate, 1);
            var (beats, downbeats) = beatgridFitter.SynthesizeFullGrid(bpm, rawBeats, rawDownbeats, durationSec);

            reporter.Complete(filePath, AnalysisSteps.Beats,
                $"{bpm:F1} BPM, {downbeats.Length} downbeats / {beats.Length} beats (from {rawDownbeats.Length}/{rawBeats.Length} raw)");
            return new BasicAnalysis(peaks, bpm, beats, downbeats);
        }
        catch (Exception ex)
        {
            reporter.Failed(filePath, AnalysisSteps.Beats, ex.Message);
            throw;
        }
    }
}

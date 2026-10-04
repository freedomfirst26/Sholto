using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;

namespace Sholto.App.Analysis.Stages;

/// <summary>
/// Computes a <see cref="BasicAnalysis"/> from a decoded track: waveform peaks plus
/// BPM/beats/downbeats from the beat analysis step, synthesized into a
/// constant-spacing beatgrid. Was previously a static factory method on
/// <see cref="BasicAnalysis"/> itself; split out so the four collaborators below
/// can be substituted by a test/Bench harness.
/// </summary>
public sealed class BasicAnalysisStage(
    IBeatAnalysisStep beatAnalyzer,
    IWaveformPeakAnalyzer peakAnalyzer,
    IBeatgridAnalyzer beatgridAnalyzer,
    IAnalysisReporter reporter) : IAnalysisStage<BasicAnalysis>
{
    private readonly IBeatAnalysisStep _beatAnalyzer = beatAnalyzer;
    private readonly IWaveformPeakAnalyzer _peakAnalyzer = peakAnalyzer;
    private readonly IBeatgridAnalyzer _beatgridAnalyzer = beatgridAnalyzer;
    private readonly IAnalysisReporter _reporter = reporter;

    public bool IsAvailable => true;

    /// <summary>
    /// Build waveform peaks from the decoded samples and run the beat analysis
    /// step for BPM + beats + downbeats. Reports progress through the reporter
    /// this instance was constructed with.
    /// </summary>
    public async Task<BasicAnalysis> RunAsync(
        DecodedTrack track,
        CancellationToken ct = default)
    {
        var filePath = track.FilePath;
        var stereoSamples = track.StereoSamples;
        var sampleRate = track.SampleRate;
        var channels = track.Channels;

        _reporter.Running(filePath, AnalysisSteps.Waveform);
        var peaks = _peakAnalyzer.Compute(stereoSamples, channels, sampleRate);
        _reporter.Complete(filePath, AnalysisSteps.Waveform);

        _reporter.Running(filePath, AnalysisSteps.Beats);
        try
        {
            var (bpm, rawBeats, rawDownbeats) = await _beatAnalyzer.AnalyzeAsync(filePath, ct);

            // Replace the beat analysis step's raw beat + downbeat detections
            // with a constant-spacing beatgrid derived from the song's BPM and
            // the densest-cluster phase anchor. Every Nth synthesized beat is a
            // synthesized downbeat by construction — guarantees the waveform's
            // small beat ticks always coincide with the tall downbeat bars, and
            // gives sync / quantised-loops a single canonical grid.
            double durationSec = stereoSamples.Length / (double)Math.Max(channels, 1) / Math.Max(sampleRate, 1);
            var (beats, downbeats) = _beatgridAnalyzer.FromDetections(bpm, rawBeats, rawDownbeats, durationSec);

            _reporter.Complete(filePath, AnalysisSteps.Beats,
                $"{bpm:F1} BPM, {downbeats.Length} downbeats / {beats.Length} beats (from {rawDownbeats.Length}/{rawBeats.Length} raw)");
            return new BasicAnalysis(peaks, bpm, beats, downbeats);
        }
        catch (Exception ex)
        {
            _reporter.Failed(filePath, AnalysisSteps.Beats, ex.Message);
            throw;
        }
    }
}

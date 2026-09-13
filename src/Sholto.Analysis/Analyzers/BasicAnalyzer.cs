using Sholto.Analysis.Analyzers.Beats;
using Sholto.Analysis.Processing;
using Sholto.Analysis.Reporting;
using Sholto.Analysis.Analyzers.Waveform;

namespace Sholto.Analysis.Analyzers;

/// <summary>
/// Computes a <see cref="BasicAnalysis"/> from a decoded track: waveform peaks plus
/// BPM/beats/downbeats from the beat analysis step, synthesized into a
/// constant-spacing beatgrid. Was previously a static factory method on
/// <see cref="BasicAnalysis"/> itself; split out so the four collaborators below
/// can be substituted by a test/Bench harness.
/// </summary>
public sealed class BasicAnalyzer : IBasicAnalyzer
{
    private readonly IBeatAnalysisStep _beatAnalyzer;
    private readonly IWaveformPeakAnalyzer _peakAnalyzer;
    private readonly IBeatgridFitter _beatgridFitter;
    private readonly IAnalysisReporter _reporter;

    public BasicAnalyzer(
        IBeatAnalysisStep beatAnalyzer,
        IWaveformPeakAnalyzer peakAnalyzer,
        IBeatgridFitter beatgridFitter,
        IAnalysisReporter reporter)
    {
        _beatAnalyzer = beatAnalyzer;
        _peakAnalyzer = peakAnalyzer;
        _beatgridFitter = beatgridFitter;
        _reporter = reporter;
    }

    /// <summary>
    /// Build waveform peaks from the decoded samples and run the beat analysis
    /// step for BPM + beats + downbeats. Reports progress through the reporter
    /// this instance was constructed with.
    /// </summary>
    public async Task<BasicAnalysis> ComputeAsync(
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
            var (beats, downbeats) = _beatgridFitter.SynthesizeFullGrid(bpm, rawBeats, rawDownbeats, durationSec);

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

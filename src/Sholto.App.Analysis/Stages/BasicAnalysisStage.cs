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
    IAnalysisReporter reporter) : IBasicAnalysisStage
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
    public Task<BasicAnalysis> RunAsync(
        DecodedTrack track,
        CancellationToken ct = default) =>
        Begin(track.FilePath, ct).CompleteAsync(track);

    /// <summary>Start the beat step, which needs only the file path, and return at once.</summary>
    public IBasicAnalysisRequest Begin(string filePath, CancellationToken ct = default)
    {
        _reporter.Running(filePath, AnalysisSteps.Beats);
        var beats = StartBeatsAsync(filePath, ct);
        return new BasicAnalysisRequest(track => CompleteAsync(track, beats, ct));
    }

    private async Task<DetectedBeats> StartBeatsAsync(string filePath, CancellationToken ct) =>
        await _beatAnalyzer.AnalyzeAsync(filePath, ct);

    private async Task<BasicAnalysis> CompleteAsync(
        DecodedTrack track, Task<DetectedBeats> beatsTask, CancellationToken ct)
    {
        var filePath = track.FilePath;
        var stereoSamples = track.StereoSamples;
        var sampleRate = track.SampleRate;
        var channels = track.Channels;

        // Peaks (CPU, ~0.6 s on a 6 min track) and the beat step (a subprocess, ~7.5 s) share no
        // data, so they run side by side: serial, the peaks were 0.6 s added to the beat grid's wait.
        // Measured: running both together costs the beat step nothing.
        var peaksTask = Task.Run(() =>
        {
            _reporter.Running(filePath, AnalysisSteps.Waveform);
            try
            {
                var computed = _peakAnalyzer.Compute(stereoSamples, channels, sampleRate);
                _reporter.Complete(filePath, AnalysisSteps.Waveform);
                return computed;
            }
            catch (Exception ex)
            {
                _reporter.Failed(filePath, AnalysisSteps.Waveform, ex.Message);
                throw;
            }
        }, ct);

        // Only the beat step and the grid are inside this try, so a catch here always means Beats failed.
        // Peaks failures are reported as Waveform by the lambda above and surface at the await below.
        double bpm;
        double[] beats;
        double[] downbeats;
        try
        {
            double[] rawBeats, rawDownbeats;
            (bpm, rawBeats, rawDownbeats) = await beatsTask;

            // Replace the beat analysis step's raw beat + downbeat detections
            // with a constant-spacing beatgrid derived from the song's BPM and
            // the densest-cluster phase anchor. Every Nth synthesized beat is a
            // synthesized downbeat by construction — guarantees the waveform's
            // small beat ticks always coincide with the tall downbeat bars, and
            // gives sync / quantised-loops a single canonical grid.
            double durationSec = stereoSamples.Length / (double)Math.Max(channels, 1) / Math.Max(sampleRate, 1);
            (beats, downbeats) = _beatgridAnalyzer.FromDetections(bpm, rawBeats, rawDownbeats, durationSec);

            _reporter.Complete(filePath, AnalysisSteps.Beats,
                $"{bpm:F1} BPM, {downbeats.Length} downbeats / {beats.Length} beats (from {rawDownbeats.Length}/{rawBeats.Length} raw)");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Superseded by a newer load: not a failure, and not still running either.
            _reporter.Cancelled(filePath, AnalysisSteps.Beats);
            if (_reporter.ReportFor(filePath).Steps.TryGetValue(AnalysisSteps.Waveform, out var waveform)
                && waveform.State == AnalysisState.Running)
                _reporter.Cancelled(filePath, AnalysisSteps.Waveform);
            throw;
        }
        catch (Exception ex)
        {
            _reporter.Failed(filePath, AnalysisSteps.Beats, ex.Message);
            throw;
        }

        return new BasicAnalysis(await peaksTask, bpm, beats, downbeats);
    }
}

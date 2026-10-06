using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stages;

namespace Sholto.App.Analysis.Tests;

public class BasicAnalysisCancellationTests
{
    /// <summary>A beat step that waits until its token is cancelled, like a killed madmom run.</summary>
    private sealed class CancellableBeatStep : IBeatAnalysisStep
    {
        public string StepName => "beats";
        public bool IsAvailable => true;
        public volatile bool Started;

        public async Task<DetectedBeats> AnalyzeAsync(string filePath, CancellationToken ct = default)
        {
            Started = true;
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        }
    }

    [Fact]
    public async Task A_cancelled_run_does_not_mark_the_track_as_failed()
    {
        const string path = "/music/a.mp3";
        var reporter = new AnalysisReporter(new[] { AnalysisSteps.Beats });
        var beats = new CancellableBeatStep();
        var peaks = new RecordingPeakAnalyzer(() => beats.Started);
        var stage = new BasicAnalysisStage(
            beats, peaks, new BeatgridAnalyzer(new BeatgridFactory()), reporter);
        using var cts = new CancellationTokenSource();

        var request = stage.Begin(path, cts.Token);
        var run = request.CompleteAsync(new DecodedTrack(path, new float[4096], 48000, 2));
        Assert.True(SpinWait.SpinUntil(() => beats.Started, 5000), "the beat step never started");

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        var report = reporter.ReportFor(path);
        Assert.False(report.HasFailure);
        Assert.False(report.HasRequiredFailure);
        Assert.False(report.Steps[AnalysisSteps.Beats].State == AnalysisState.Running);
        Assert.Equal(AnalysisState.NotStarted, report.Steps[AnalysisSteps.Beats].State);
        Assert.False(report.IsBusy);
    }
}

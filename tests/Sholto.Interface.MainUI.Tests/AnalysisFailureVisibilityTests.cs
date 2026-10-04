using Sholto.App.Analysis;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Library;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

public class AnalysisFailureStateTests
{
    private TrackRow NewRow(string path = "/music/track.mp3")
    {
        AvaloniaTestApp.EnsureStarted();
        return new(new Track(path, "Title", "Artist", TimeSpan.FromMinutes(5)).ToSummary(), new ThemeStackFactory().Build().Context);
    }

    [Fact]
    public void A_failed_step_is_reported_as_a_failure_not_merely_as_not_busy()
    {
        const string path = "/music/track.mp3";
        var reporter = new AnalysisReporter(Array.Empty<string>());
        reporter.Running(path, "stems", 0.5, "50%");
        var report = reporter.ReportFor(path);
        Assert.True(report.IsBusy);
        Assert.False(report.HasFailure);

        reporter.Failed(path, "stems", "demucs exited with code 1\nImportError: TorchCodec");

        // This is the crux: IsBusy going false looks exactly like success.
        Assert.False(report.IsBusy);
        Assert.True(report.HasFailure);
        Assert.Contains("ImportError: TorchCodec", report.FailureMessage);
        Assert.StartsWith("stems:", report.FailureMessage);
    }

    [Fact]
    public void Reporter_raises_Updated_when_a_step_fails()
    {
        const string path = "/music/track.mp3";
        var reporter = new AnalysisReporter(Array.Empty<string>());
        string? seen = null;
        reporter.Updated += r => seen = r.FailureMessage;

        reporter.Failed(path, "stems", "demucs exited with code 1");

        Assert.NotNull(seen);
        Assert.Contains("demucs exited with code 1", seen);
    }

    [Fact]
    public void Row_shows_the_failure_marker_instead_of_nothing()
    {
        var row = NewRow();
        Assert.Equal(TrackAnalysisState.Unanalyzed, row.AnalysisState);
        Assert.False(row.ShowAnalysisFailed);

        row.AnalysisFailure = "stems: demucs exited with code 1";

        Assert.Equal(TrackAnalysisState.Failed, row.AnalysisState);
        Assert.True(row.ShowAnalysisFailed);
        Assert.False(row.ShowAnalyzedCheck);
        Assert.Contains("demucs exited with code 1", row.AnalysisFailureTooltip);
    }

    [Fact]
    public void A_running_step_outranks_a_previous_failure()
    {
        var row = NewRow();
        row.AnalysisFailure = "stems: boom";
        row.IsAnalyzing = true;

        Assert.Equal(TrackAnalysisState.Analyzing, row.AnalysisState);
        Assert.False(row.ShowAnalysisFailed);
    }

    [Fact]
    public void A_fully_analysed_track_still_shows_the_tick_when_an_optional_step_failed()
    {
        var row = NewRow();
        row.Bpm = 128;
        row.StemsReady = true;
        row.AnalysisFailure = "segments: analyzer exit 1";
        // HasRequiredFailure deliberately left false — segments analysis is optional.

        Assert.Equal(TrackAnalysisState.Analyzed, row.AnalysisState);
        Assert.True(row.ShowAnalyzedCheck);
        Assert.False(row.ShowAnalysisFailed);
    }

    [Fact]
    public void A_required_step_failure_outranks_the_tick_even_when_fully_analysed()
    {
        // The bug this guards: a track with cached BPM + stems from a previous
        // successful run re-analyses with a broken beat tracker. AnalysisFailure
        // alone used to rank BELOW Analyzed, so the row kept showing the green
        // tick even though the beatgrid the tick promises never got regenerated.
        var row = NewRow();
        row.Bpm = 128;
        row.StemsReady = true;
        row.AnalysisFailure = "beats: madmom exit 1";
        row.HasRequiredFailure = true;

        Assert.Equal(TrackAnalysisState.Failed, row.AnalysisState);
        Assert.True(row.ShowAnalysisFailed);
        Assert.False(row.ShowAnalyzedCheck);
    }

    [Fact]
    public void Clearing_the_failure_returns_the_row_to_unanalysed()
    {
        var row = NewRow();
        row.AnalysisFailure = "stems: boom";
        row.AnalysisFailure = null;

        Assert.Equal(TrackAnalysisState.Unanalyzed, row.AnalysisState);
    }

    [Fact]
    public void Row_notifies_the_view_when_the_failure_appears()
    {
        var row = NewRow();
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.AnalysisFailure = "stems: boom";

        Assert.Contains(nameof(TrackRow.ShowAnalysisFailed), changed);
        Assert.Contains(nameof(TrackRow.AnalysisFailureTooltip), changed);
    }
}

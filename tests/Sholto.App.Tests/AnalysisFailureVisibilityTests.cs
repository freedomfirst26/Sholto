using Sholto.Analysis;
using Sholto.App.Theming;
using Sholto.App.ViewModels;
using Sholto.Library;

namespace Sholto.App.Tests;

/// <summary>
/// The failure path that a broken demucs exposed: the analyser's own error output was
/// discarded by the progress parser, and a Failed step was indistinguishable from a
/// success because nothing read anything but IsBusy.
/// </summary>
public class ProcessOutputTailTests
{
    [Fact]
    public void Keeps_only_the_last_N_lines()
    {
        var tail = new ProcessOutputTail(capacity: 3);
        for (var i = 1; i <= 10; i++) tail.Add($"line {i}");

        Assert.Equal("line 8\nline 9\nline 10", tail.Text);
    }

    [Fact]
    public void Ignores_blank_lines()
    {
        var tail = new ProcessOutputTail();
        tail.Add(null);
        tail.Add("");
        tail.Add("   ");

        Assert.True(tail.IsEmpty);
    }

    [Fact]
    public void Truncates_a_single_enormous_line()
    {
        var tail = new ProcessOutputTail();
        tail.Add(new string('x', 100_000));

        Assert.True(tail.Text.Length <= ProcessOutputTail.MaxLineLength + 1);
    }

    [Fact]
    public void Annotate_appends_captured_output_and_leaves_the_message_alone_when_empty()
    {
        var empty = new ProcessOutputTail();
        Assert.Equal("boom", empty.Annotate("boom"));

        var tail = new ProcessOutputTail();
        tail.Add("Traceback (most recent call last):");
        tail.Add("ImportError: TorchCodec is required for save_with_torchcodec");

        var annotated = tail.Annotate("demucs exited with code 1");
        Assert.StartsWith("demucs exited with code 1", annotated);
        Assert.Contains("ImportError: TorchCodec is required", annotated);
    }

}

public class AnalysisFailureStateTests
{
    private static TrackRow NewRow(string path = "/music/track.mp3")
    {
        AvaloniaTestApp.EnsureStarted();
        return new(new Track(path, "Title", "Artist", TimeSpan.FromMinutes(5)), new ThemeContext());
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

using Sholto.App.Audio.BatchAnalysis;
using Sholto.App.Library;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests.Library;

/// <summary>Settings ▸ Analyse whole library: every catalog path goes to the batch analyser, its progress
/// comes back as <see cref="LibraryAnalysisProgressChanged"/>, one run at a time, and a cancel starts no
/// further songs.</summary>
public class LibraryAnalysisHandlerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private readonly Origin _menu = new(InterfaceIds.MainUI, "menu", "analyze-library");
    private readonly RecordingProgressEvents _events = new();
    private readonly LibraryCatalog _catalog;

    public LibraryAnalysisHandlerTests()
    {
        _catalog = new LibraryCatalog(_events);
        _catalog.ReplaceAll(
        [
            new TrackSummary("/music/a.mp3", "A", "Artist", TimeSpan.FromMinutes(3)),
            new TrackSummary("/music/b.mp3", "B", "Artist", TimeSpan.FromMinutes(3)),
            new TrackSummary("/music/c.mp3", "C", "Artist", TimeSpan.FromMinutes(3)),
            new TrackSummary("/music/d.mp3", "D", "Artist", TimeSpan.FromMinutes(3)),
        ]);
    }

    private LibraryAnalysisHandler Handler(IBatchAnalyzer? analyzer) => Handler(new FakeBatchAnalyzerFactory(analyzer));

    private LibraryAnalysisHandler Handler(FakeBatchAnalyzerFactory factory) =>
        new(_catalog, factory, new ImmediateAppThread(), _events, new RecordingLogFactory().Create("LibraryAnalysis"));

    [Fact]
    public void The_analyser_receives_every_catalog_path_in_scan_order()
    {
        var analyzer = new ScriptedBatchAnalyzer(new Dictionary<string, BatchTrackState>());

        Handler(analyzer).Handle(new AnalyzeLibrary(_menu));

        Assert.Equal(["/music/a.mp3", "/music/b.mp3", "/music/c.mp3", "/music/d.mp3"], analyzer.Paths);
    }

    [Fact]
    public async Task Skipped_and_finished_count_as_done_failed_as_failed_and_the_last_event_is_not_running()
    {
        var analyzer = new ScriptedBatchAnalyzer(new Dictionary<string, BatchTrackState>
        {
            ["/music/a.mp3"] = BatchTrackState.Skipped,
            ["/music/b.mp3"] = BatchTrackState.Failed,
        });

        Handler(analyzer).Handle(new AnalyzeLibrary(_menu));
        var last = await _events.Finished.Task.WaitAsync(Wait);

        Assert.Equal(new LibraryAnalysisProgressChanged(3, 4, 1, false), last);
        Assert.Equal(last, _events.Events[^1]);
        // Started reports are not counted: one event at the start, one per song that ended, one at the end.
        Assert.Equal(new LibraryAnalysisProgressChanged(0, 4, 0, true), _events.Events[0]);
        Assert.Equal(6, _events.Events.Count);
        Assert.All(_events.Events[..^1], e => Assert.True(e.Running));
    }

    [Fact]
    public async Task A_second_analyse_while_one_is_running_is_ignored()
    {
        var analyzer = new GatedBatchAnalyzer();
        var factory = new FakeBatchAnalyzerFactory(analyzer);
        var handler = Handler(factory);

        handler.Handle(new AnalyzeLibrary(_menu));
        await analyzer.FirstStarted.Task.WaitAsync(Wait);
        handler.Handle(new AnalyzeLibrary(_menu));

        Assert.Equal(1, analyzer.Runs);
        Assert.Equal(1, factory.Created);
        analyzer.Release();
        var last = await _events.Finished.Task.WaitAsync(Wait);
        Assert.Equal(new LibraryAnalysisProgressChanged(4, 4, 0, false), last);
    }

    [Fact]
    public async Task Cancel_starts_no_further_songs_and_ends_the_run()
    {
        var analyzer = new GatedBatchAnalyzer();
        var handler = Handler(analyzer);

        handler.Handle(new AnalyzeLibrary(_menu));
        await analyzer.FirstStarted.Task.WaitAsync(Wait);
        handler.Handle(new CancelLibraryAnalysis(_menu));
        analyzer.Release();
        var last = await _events.Finished.Task.WaitAsync(Wait);

        Assert.Equal(["/music/a.mp3"], analyzer.Started);
        Assert.Equal(new LibraryAnalysisProgressChanged(1, 4, 0, false), last);
    }

    [Fact]
    public async Task After_a_cancelled_run_a_new_analyse_starts_again()
    {
        var analyzer = new GatedBatchAnalyzer();
        var handler = Handler(analyzer);
        handler.Handle(new AnalyzeLibrary(_menu));
        await analyzer.FirstStarted.Task.WaitAsync(Wait);
        handler.Handle(new CancelLibraryAnalysis(_menu));
        analyzer.Release();
        await _events.Finished.Task.WaitAsync(Wait);

        handler.Handle(new AnalyzeLibrary(_menu));

        Assert.Equal(2, analyzer.Runs);
    }

    [Fact]
    public void Cancel_with_nothing_running_does_nothing()
    {
        Handler(new ScriptedBatchAnalyzer(new Dictionary<string, BatchTrackState>()))
            .Handle(new CancelLibraryAnalysis(_menu));

        Assert.Empty(_events.Events);
    }

    [Fact]
    public void Without_a_database_nothing_runs_and_one_empty_finished_event_is_sent()
    {
        Handler(analyzer: null).Handle(new AnalyzeLibrary(_menu));

        Assert.Equal([new LibraryAnalysisProgressChanged(0, 0, 0, false)], _events.Events);
    }
}

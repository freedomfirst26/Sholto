using Sholto.App.Audio.BatchAnalysis;
using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>
/// Analyse the whole library catalog in the background through the batch analyser (beats and key only:
/// stems stay on deck loads), and cancel it. Every catalog path is handed over; the batch analyser
/// skips the songs already analysed.
///
/// <para>Runs on the app thread. Progress comes back from worker threads and is posted onto the app
/// thread before it is counted and published as <see cref="LibraryAnalysisProgressChanged"/>: Skipped and
/// Finished count as done, Failed as failed. One run at a time; a second <see cref="AnalyzeLibrary"/>
/// while one runs is ignored.</para>
/// </summary>
public sealed class LibraryAnalysisHandler(
    ILibraryCatalog catalog,
    ILibraryBatchAnalyzerFactory analyzers,
    IAppThread appThread,
    IEventPublisher publisher) : ICommandHandler<AnalyzeLibrary>, ICommandHandler<CancelLibraryAnalysis>, IDisposable
{
    private readonly ILibraryCatalog _catalog = catalog;
    private readonly ILibraryBatchAnalyzerFactory _analyzers = analyzers;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;
    // Cancelled on Dispose; each run's source is linked to it so disposing the handler stops a run.
    private readonly CancellationTokenSource _lifetime = new();

    private CancellationTokenSource? _run;
    private int _done;
    private int _failed;
    private int _total;

    public void Handle(in AnalyzeLibrary command)
    {
        if (_run is not null)
        {
            Console.WriteLine($"[LibraryAnalysis] {command.Origin.GestureName}: already running, ignored");
            return;
        }

        var paths = _catalog.All.Select(s => s.FilePath).ToArray();
        var analyzer = _analyzers.Create();
        if (analyzer is null)
        {
            Console.WriteLine($"[LibraryAnalysis] {command.Origin.GestureName}: no library database yet, nothing analysed");
            _publisher.Publish(new LibraryAnalysisProgressChanged(0, 0, 0, false));
            return;
        }

        _run = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _done = 0;
        _failed = 0;
        _total = paths.Length;
        Console.WriteLine($"[LibraryAnalysis] {command.Origin.GestureName}: {paths.Length} songs");
        Publish();
        _ = RunAsync(analyzer, paths, _run);
    }

    public void Handle(in CancelLibraryAnalysis command)
    {
        if (_run is null) return;
        Console.WriteLine($"[LibraryAnalysis] {command.Origin.GestureName}: cancelling");
        _run.Cancel();
    }

    public void Dispose() => _lifetime.Cancel();

    private async Task RunAsync(IBatchAnalyzer analyzer, IReadOnlyList<string> paths, CancellationTokenSource run)
    {
        try
        {
            var progress = new AppThreadProgress<BatchTrackProgress>(_appThread, OnTrack);
            await analyzer.RunAsync(paths, progress, run.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[LibraryAnalysis] run failed: {ex.Message}");
        }
        // Posted after every progress report of the run, so the last event carries the final counts.
        _appThread.Post(() => Finish(run));
    }

    private void OnTrack(BatchTrackProgress p)
    {
        switch (p.State)
        {
            case BatchTrackState.Finished:
            case BatchTrackState.Skipped:
                _done++;
                break;
            case BatchTrackState.Failed:
                _failed++;
                Console.WriteLine($"[LibraryAnalysis] failed {p.Path}: {p.Error}");
                break;
            default:
                return;
        }
        Publish();
    }

    private void Finish(CancellationTokenSource run)
    {
        run.Dispose();
        _run = null;
        Console.WriteLine($"[LibraryAnalysis] done: {_done} of {_total}, {_failed} failed");
        Publish();
    }

    private void Publish() => _publisher.Publish(new LibraryAnalysisProgressChanged(_done, _total, _failed, _run is not null));
}

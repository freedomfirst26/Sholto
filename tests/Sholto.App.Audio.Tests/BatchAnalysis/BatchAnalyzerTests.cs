using Microsoft.Extensions.Options;
using Sholto.App.Audio.BatchAnalysis;

namespace Sholto.App.Audio.Tests.BatchAnalysis;

public class BatchAnalyzerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly CountingDecoder _decoder = new();
    private readonly FakeBasicAnalysisStore _basicStore = new();
    private readonly FakeKeyAnalysisStore _keyStore = new();

    private BatchAnalyzer Analyzer(GatedAnalysisProvider provider, GatedKeyAnalyzer keys, int maxParallel) =>
        new(_decoder, provider, _basicStore, _keyStore, keys,
            Options.Create(new BatchAnalysisOptions { MaxParallelTracks = maxParallel }),
            new RecordingLogFactory().Create("Batch"));

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("condition not met");
            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task Never_more_than_max_parallel_tracks_in_flight_and_every_path_finishes()
    {
        var provider = new GatedAnalysisProvider();
        var paths = Enumerable.Range(0, 6).Select(i => $"/music/{i}.mp3").ToList();
        var log = new ProgressLog();

        var run = Analyzer(provider, new GatedKeyAnalyzer(), maxParallel: 2).RunAsync(paths, log, CancellationToken.None);

        // Fill both slots, give a third track the chance to sneak in, then let them through one at a time.
        await WaitUntil(() => provider.InFlight == 2);
        await Task.Delay(100);
        Assert.Equal(2, provider.InFlight);
        for (int i = 0; i < paths.Count; i++)
        {
            provider.Release(1);
            await Task.Delay(20);
            Assert.True(provider.InFlight <= 2);
        }
        await run.WaitAsync(Timeout);

        Assert.Equal(2, provider.MaxInFlight);
        foreach (var path in paths)
        {
            Assert.Equal([BatchTrackState.Started, BatchTrackState.Finished], log.StatesFor(path));
            Assert.Equal(1, _decoder.CallsFor(path));
            Assert.True(_keyStore.Has(path));
        }
    }

    [Fact]
    public async Task A_track_with_beats_and_key_stored_is_skipped_without_decoding()
    {
        const string stored = "/music/stored.mp3", keyMissing = "/music/key-missing.mp3", fresh = "/music/fresh.mp3";
        _basicStore.Store(stored);
        _keyStore.Store(stored);
        _basicStore.Store(keyMissing);
        var provider = new GatedAnalysisProvider();
        provider.Release(10);
        var log = new ProgressLog();

        await Analyzer(provider, new GatedKeyAnalyzer(), maxParallel: 2)
            .RunAsync([stored, keyMissing, fresh], log, CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal(0, _decoder.CallsFor(stored));
        Assert.False(provider.Tokens.ContainsKey(stored));
        Assert.Equal([BatchTrackState.Started, BatchTrackState.Skipped], log.StatesFor(stored));
        // Beats alone are not enough: the key still needs the samples.
        Assert.Equal(1, _decoder.CallsFor(keyMissing));
        Assert.True(_keyStore.Has(keyMissing));
        Assert.Equal([BatchTrackState.Started, BatchTrackState.Finished], log.StatesFor(keyMissing));
        Assert.Equal([BatchTrackState.Started, BatchTrackState.Finished], log.StatesFor(fresh));
    }

    [Fact]
    public async Task One_track_throwing_is_reported_failed_with_its_message_and_the_others_finish()
    {
        const string bad = "/music/bad.mp3";
        var provider = new GatedAnalysisProvider { FailingPath = bad };
        provider.Release(10);
        var paths = new[] { "/music/a.mp3", bad, "/music/b.mp3", "/music/c.mp3" };
        var log = new ProgressLog();

        await Analyzer(provider, new GatedKeyAnalyzer(), maxParallel: 1).RunAsync(paths, log, CancellationToken.None).WaitAsync(Timeout);

        var badReports = log.For(bad);
        Assert.Equal([BatchTrackState.Started, BatchTrackState.Failed], badReports.Select(r => r.State));
        Assert.Equal($"boom: {bad}", badReports[1].Error);
        foreach (var path in paths.Where(p => p != bad))
        {
            Assert.Equal([BatchTrackState.Started, BatchTrackState.Finished], log.StatesFor(path));
            Assert.Null(log.For(path)[1].Error);
        }
    }

    [Fact]
    public async Task Cancelling_mid_batch_cancels_the_running_track_and_starts_no_more()
    {
        var provider = new GatedAnalysisProvider();
        var keys = new GatedKeyAnalyzer { Hold = true };
        var paths = Enumerable.Range(0, 4).Select(i => $"/music/{i}.mp3").ToList();
        var log = new ProgressLog();
        using var cts = new CancellationTokenSource();

        var run = Analyzer(provider, keys, maxParallel: 1).RunAsync(paths, log, cts.Token);
        await WaitUntil(() => provider.InFlight == 1 && keys.Tokens.ContainsKey(paths[0]));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Timeout));

        Assert.True(provider.Tokens[paths[0]].IsCancellationRequested);
        Assert.True(keys.Tokens[paths[0]].IsCancellationRequested);
        Assert.Equal([BatchTrackState.Started, BatchTrackState.Failed], log.StatesFor(paths[0]));
        foreach (var path in paths.Skip(1))
        {
            Assert.Empty(log.For(path));
            Assert.Equal(0, _decoder.CallsFor(path));
        }
    }
}

using Sholto.App.Library;
using System.Diagnostics;
using Sholto.App.Glance;
using Sholto.Data;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Sholto.App.Tests.Glance;

/// <summary>The <see cref="RankTracks"/> query over a real library session and scripted decks: what it ranks, what
/// it ranks against, and that the ranking runs off the app thread.</summary>
public class RankTracksHandlerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private readonly GlanceTrackBuilder _keys = new();

    private static readonly Track Ref = new("/music/ref.mp3", "Ref", "Mid", TimeSpan.FromMinutes(5));
    private static readonly Track Match = new("/music/match.mp3", "Match", "Mid", TimeSpan.FromMinutes(5));
    private static readonly Track Clash = new("/music/clash.mp3", "Clash", "Mid", TimeSpan.FromMinutes(5));

    private static RankTracksHandler HandlerFor(GlanceHandlerRig rig, int maxResults = 250) =>
        new(rig.Library.Library, rig.Decks, new GlanceRanker(new FitScorer(), new GlanceQueryFactory(), new GlanceMatcher()),
            new GlanceScope(), new CrateMembershipCache(rig.Library.Library),
            Options.Create(new GlanceOptions { MaxResults = maxResults }));

    private static GlanceHandlerRig RigWithFacts()
    {
        var rig = new GlanceHandlerRig(Ref, Match, Clash);
        rig.Seed(new Dictionary<string, (string, double)>
        {
            [Ref.FilePath] = ("8A", 125.5),
            [Match.FilePath] = ("8A", 128.0),
            [Clash.FilePath] = ("3B", 90.0),
        });
        return rig;
    }

    [Fact]
    public async Task Ranks_the_rows_against_the_deck_that_is_not_the_target()
    {
        var rig = RigWithFacts();
        // Deck 1 plays Ref: source 125.5 at +2 % = 128.01 effective.
        rig.Load(0, Ref, sourceBpm: 125.5, camelot: "8A", playing: true, positionSec: 30, speed: 1.02);

        var result = await HandlerFor(rig).Handle(new RankTracks("", 1));

        Assert.True(result.FitActive);
        Assert.Equal(0, result.ReferenceDeck);
        Assert.Equal(_keys.Key("8A"), result.ReferenceKey);
        Assert.InRange(result.ReferenceBpm!.Value, 128.00, 128.02);
        Assert.Equal(new[] { "Match", "Clash", "Ref" }, result.Rows.Select(r => r.Summary.Title));
        Assert.Equal(FitLevel.Good, result.Rows[0].Fit);
        Assert.Equal(FitLevel.Clash, result.Rows[1].Fit);
        Assert.True(result.Rows[2].IsReference);
    }

    [Fact]
    public async Task Targeting_the_loaded_deck_ranks_against_the_empty_one_so_fit_is_off()
    {
        var rig = RigWithFacts();
        rig.Load(0, Ref, sourceBpm: 125.5, camelot: "8A", playing: true, speed: 1.02);

        var result = await HandlerFor(rig).Handle(new RankTracks("", 0));

        Assert.False(result.FitActive);
        Assert.Equal(-1, result.ReferenceDeck);
        Assert.All(result.Rows, r => Assert.Equal(FitLevel.None, r.Fit));
    }

    [Fact]
    public async Task Both_decks_empty_turns_fit_off_and_sorts_by_artist_then_title()
    {
        var rig = new GlanceHandlerRig();

        var result = await HandlerFor(rig).Handle(new RankTracks("", 0));

        Assert.False(result.FitActive);
        Assert.Equal(-1, result.ReferenceDeck);
        // Abe/Bravo, Abe/Charlie, Zed/Alpha.
        Assert.Equal(new[] { "Bravo", "Charlie", "Alpha" }, result.Rows.Select(r => r.Summary.Title));
    }

    [Fact]
    public async Task A_reference_deck_still_without_a_key_is_named_but_fit_stays_off()
    {
        var rig = RigWithFacts();
        rig.Load(0, Ref, sourceBpm: 125.5);

        var result = await HandlerFor(rig).Handle(new RankTracks("", 1));

        Assert.False(result.FitActive);
        Assert.Equal(0, result.ReferenceDeck);
        Assert.Null(result.ReferenceKey);
    }

    [Fact]
    public async Task The_whole_catalogue_is_ranked_not_just_the_visible_rows()
    {
        var rig = new GlanceHandlerRig();
        var lib = rig.Library;
        var alphaId = lib.Catalog.Assign(LibrarySessionRig.Alpha.FilePath);
        var tags = new FakeTagService(new Dictionary<Guid, IReadOnlyList<string>> { [alphaId] = new[] { "house" } });
        await Task.Run(() => lib.Library.ScanAsync("/music", lib.Stack(tags: tags)));
        lib.Library.AttachServices(tags, lib.Crates);
        await lib.Library.FilterByTagAsync("house");
        Assert.Single(lib.Library.Rows);

        var result = await HandlerFor(rig).Handle(new RankTracks("", 0));

        Assert.Equal(new[] { "Bravo", "Charlie", "Alpha" }, result.Rows.Select(r => r.Summary.Title));
    }

    [Fact]
    public async Task The_query_text_filters_the_rows()
    {
        var rig = RigWithFacts();

        var result = await HandlerFor(rig).Handle(new RankTracks("clash", 0));

        Assert.Equal(new[] { "Clash" }, result.Rows.Select(r => r.Summary.Title));
    }

    [Fact]
    public async Task The_ranking_runs_off_the_calling_thread_but_the_snapshot_is_taken_on_it()
    {
        var rig = RigWithFacts();
        rig.Load(0, Ref, sourceBpm: 125.5, camelot: "8A");
        var spy = new SpyRanker();
        var handler = new RankTracksHandler(rig.Library.Library, rig.Decks, spy, new GlanceScope(),
            new CrateMembershipCache(rig.Library.Library), Options.Create(new GlanceOptions()));
        var callerThread = Environment.CurrentManagedThreadId;

        var pending = handler.Handle(new RankTracks("", 1));
        await pending;

        Assert.NotEqual(callerThread, spy.RankedOnThread);
        Assert.Equal(3, spy.RowCount);
        Assert.Equal(0, spy.Reference!.Value.Deck);
    }

    [Fact]
    public async Task Ranking_a_synthetic_ten_thousand_track_library_is_timed()
    {
        const int Count = 10_000;
        var tracks = Enumerable.Range(0, Count)
            .Select(i => new Track($"/music/t{i}.mp3", $"Title {i}", $"Artist {i % 400}", TimeSpan.FromMinutes(4)))
            .ToArray();
        var rig = new GlanceHandlerRig(tracks);
        rig.Seed(tracks.Select((t, i) => (t, i)).ToDictionary(
            x => x.t.FilePath,
            x => ($"{1 + x.i % 12}{(x.i % 2 == 0 ? 'A' : 'B')}", 100.0 + x.i % 60)));
        rig.Load(0, tracks[0], sourceBpm: 126, camelot: "8A", playing: true);
        var handler = HandlerFor(rig, Count);

        await handler.Handle(new RankTracks("", 1)); // warm-up (JIT)
        var elapsed = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            var result = await handler.Handle(new RankTracks("", 1));
            watch.Stop();
            elapsed.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(Count, result.Rows.Count);
        }
        var queryWatch = Stopwatch.StartNew();
        var filtered = await handler.Handle(new RankTracks("bpm:120-124 key:7a", 1));
        queryWatch.Stop();

        _output.WriteLine($"RankTracks over {Count} rows, fit active: min {elapsed.Min():F1} ms, median {elapsed.Order().ElementAt(2):F1} ms, max {elapsed.Max():F1} ms");
        _output.WriteLine($"RankTracks with filters bpm:120-124 key:7a: {queryWatch.Elapsed.TotalMilliseconds:F1} ms, {filtered.Rows.Count} rows");
        Assert.True(elapsed.Max() < 2000, $"ranking 10k rows took {elapsed.Max():F0} ms");
    }

    private sealed class SpyRanker : IGlanceRanker
    {
        public int RankedOnThread { get; private set; }
        public int RowCount { get; private set; }
        public GlanceReference? Reference { get; private set; }

        public RankedTracks Rank(IReadOnlyList<TrackSummary> rows, GlanceReference? reference, string query)
        {
            RankedOnThread = Environment.CurrentManagedThreadId;
            RowCount = rows.Count;
            Reference = reference;
            return new RankedTracks([], reference?.Deck ?? -1, reference?.Key, reference?.Bpm, false, []);
        }
    }
}

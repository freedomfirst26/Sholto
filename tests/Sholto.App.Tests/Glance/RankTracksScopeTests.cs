using System.Diagnostics;
using Microsoft.Extensions.Options;
using Sholto.App.Glance;
using Sholto.App.Library;
using Sholto.App.Library.Crates;
using Sholto.Data;
using Xunit.Abstractions;

namespace Sholto.App.Tests.Glance;

/// <summary>The <see cref="RankTracks"/> chips (crates and tags, ANDed), the scope counts, the result cap and the
/// crate-membership cache, over a real library session and the in-memory crate and tag fakes.</summary>
public class RankTracksScopeTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private readonly Origin _from = new(InterfaceIds.Bench, "test", "glance");

    private static readonly Track A = new("/music/a.mp3", "A", "Art", TimeSpan.FromMinutes(3));
    private static readonly Track B = new("/music/b.mp3", "B", "Art", TimeSpan.FromMinutes(3));
    private static readonly Track C = new("/music/c.mp3", "C", "Art", TimeSpan.FromMinutes(3));
    private static readonly Track D = new("/music/d.mp3", "D", "Art", TimeSpan.FromMinutes(3));
    private static readonly Track E = new("/music/e.mp3", "E", "Art", TimeSpan.FromMinutes(3));

    /// <summary>Crates Peak {A,B}, Warm {B,C}, All Tracks {A..E}. Tags: A "Vocal", C "vocal", D "tech house", E "House".</summary>
    private sealed class Scene
    {
        public required GlanceHandlerRig Rig { get; init; }
        public required RankTracksHandler Handler { get; init; }
        public required LibraryCommandHandlers Commands { get; init; }
        public int Peak { get; init; }
        public int Warm { get; init; }
        public int AllTracks { get; init; }
        public Dictionary<string, Guid> Ids { get; init; } = [];
        public FakeCrateService Crates => Rig.Library.Crates;
    }

    private async Task<Scene> BuildAsync(int maxResults = 250)
    {
        var tracks = new[] { A, B, C, D, E };
        var rig = new GlanceHandlerRig(tracks);
        var lib = rig.Library;
        var ids = tracks.ToDictionary(t => t.Title, t => lib.Catalog.Assign(t.FilePath));
        var tags = new FakeTagService(new Dictionary<Guid, IReadOnlyList<string>>
        {
            [ids["A"]] = ["Vocal"], [ids["C"]] = ["vocal"], [ids["D"]] = ["tech house"], [ids["E"]] = ["House"],
        });
        await Task.Run(() => lib.Library.ScanAsync("/music", lib.Stack(tags: tags)));
        lib.Library.AttachServices(tags, lib.Crates);
        var peak = await lib.Crates.CreateAsync("Peak");
        var warm = await lib.Crates.CreateAsync("Warm");
        var all = await lib.Crates.CreateAsync(CrateNames.AllTracks);
        foreach (var t in new[] { "A", "B" }) await lib.Crates.AddTrackAsync(peak, ids[t]);
        foreach (var t in new[] { "B", "C" }) await lib.Crates.AddTrackAsync(warm, ids[t]);
        foreach (var t in tracks) await lib.Crates.AddTrackAsync(all, ids[t.Title]);

        var cache = new CrateMembershipCache(lib.Library);
        return new Scene
        {
            Rig = rig,
            Handler = new RankTracksHandler(lib.Library, rig.Decks,
                new GlanceRanker(new FitScorer(Options.Create(new GlanceOptions())), new GlanceQueryFactory(), new GlanceMatcher()), new GlanceScope(), cache,
                Options.Create(new GlanceOptions { MaxResults = maxResults })),
            Commands = new LibraryCommandHandlers(lib.Library, new ImmediateAppThread(), lib.Bus, cache),
            Peak = peak, Warm = warm, AllTracks = all, Ids = ids,
        };
    }

    private static string[] Titles(RankedTracks r) => r.Rows.Select(x => x.Summary.Title).Order().ToArray();

    [Fact]
    public async Task A_crate_chip_keeps_only_its_members_and_counts_them()
    {
        var s = await BuildAsync();

        var r = await s.Handler.Handle(new RankTracks("", 0) { CrateIds = [s.Peak] });

        Assert.Equal(["A", "B"], Titles(r));
        Assert.Equal(2, r.ScopeCount);
    }

    [Fact]
    public async Task Crate_and_tag_chips_are_ANDed()
    {
        var s = await BuildAsync();

        var r = await s.Handler.Handle(new RankTracks("", 0) { CrateIds = [s.Peak], Tags = ["Vocal"] });

        Assert.Equal(["A"], Titles(r));
        Assert.Equal(1, r.ScopeCount);
    }

    [Fact]
    public async Task Two_crate_chips_keep_the_intersection()
    {
        var s = await BuildAsync();

        var r = await s.Handler.Handle(new RankTracks("", 0) { CrateIds = [s.Peak, s.Warm] });

        Assert.Equal(["B"], Titles(r));
    }

    [Fact]
    public async Task A_tag_chip_is_an_exact_name_ignoring_case_not_a_substring()
    {
        var s = await BuildAsync();

        var r = await s.Handler.Handle(new RankTracks("", 0) { Tags = ["house"] });

        Assert.Equal(["E"], Titles(r));
    }

    [Fact]
    public async Task All_Tracks_is_never_a_scope()
    {
        var s = await BuildAsync();

        var all = await s.Handler.Handle(new RankTracks("", 0) { CrateIds = [s.AllTracks] });
        var withPeak = await s.Handler.Handle(new RankTracks("", 0) { CrateIds = [s.AllTracks, s.Peak] });

        Assert.Equal(["A", "B", "C", "D", "E"], Titles(all));
        Assert.Equal(5, all.ScopeCount);
        Assert.Equal(["A", "B"], Titles(withPeak));
    }

    [Fact]
    public async Task An_unknown_crate_id_leaves_nothing()
    {
        var s = await BuildAsync();

        var r = await s.Handler.Handle(new RankTracks("", 0) { CrateIds = [999] });

        Assert.Empty(r.Rows);
        Assert.Equal(0, r.ScopeCount);
    }

    [Fact]
    public async Task Rail_counts_are_what_each_item_leaves_given_the_chips_and_ignore_the_text()
    {
        var s = await BuildAsync();

        var r = await s.Handler.Handle(new RankTracks("zzz", 0) { CrateIds = [s.Peak] });

        Assert.Empty(r.Rows);
        Assert.Equal(2, r.ScopeCount);
        Assert.Equal(2, r.ScopeCrateCounts![s.Peak]);
        Assert.Equal(1, r.ScopeCrateCounts[s.Warm]);
        Assert.False(r.ScopeCrateCounts.ContainsKey(s.AllTracks));
        Assert.Equal(1, r.ScopeTagCounts!["VOCAL"]);
        Assert.False(r.ScopeTagCounts.ContainsKey("house"));
    }

    [Fact]
    public async Task With_no_chips_the_scope_is_the_catalogue_and_the_counts_are_null()
    {
        var s = await BuildAsync();

        var r = await s.Handler.Handle(new RankTracks("", 0));

        Assert.Equal(5, r.ScopeCount);
        Assert.Null(r.ScopeCrateCounts);
        Assert.Null(r.ScopeTagCounts);
        Assert.Equal(0, s.Crates.MembershipCalls);
    }

    [Fact]
    public async Task Ranked_rows_can_come_from_outside_the_visible_library_rows()
    {
        var s = await BuildAsync();
        s.Rig.Library.Library.ShowTrackList(s.Rig.Library.Library.Catalog.Where(x => x.Title is "B" or "C").Select(x => x.FilePath).ToList());
        Assert.Equal(["B", "C"], s.Rig.Library.Library.Rows.Select(x => x.Title).Order());

        var r = await s.Handler.Handle(new RankTracks("", 0) { CrateIds = [s.Peak] });

        Assert.Equal(["A", "B"], Titles(r));
    }

    [Fact]
    public async Task The_order_inside_the_scope_is_the_rankers_order_over_that_subset()
    {
        var s = await BuildAsync();
        var ranker = new GlanceRanker(new FitScorer(Options.Create(new GlanceOptions())), new GlanceQueryFactory(), new GlanceMatcher());
        var subset = s.Rig.Library.Library.Catalog.Where(x => x.Title is "A" or "B").ToList();

        var r = await s.Handler.Handle(new RankTracks("", 0) { CrateIds = [s.Peak] });

        Assert.Equal(ranker.Rank(subset, null, "").Rows.Select(x => x.Summary.Title), r.Rows.Select(x => x.Summary.Title));
    }

    [Fact]
    public async Task The_cap_returns_at_most_MaxResults_rows_while_ScopeCount_stays_the_full_count()
    {
        var s = await BuildAsync(maxResults: 2);

        var r = await s.Handler.Handle(new RankTracks("", 0));

        Assert.Equal(2, r.Rows.Count);
        Assert.Equal(5, r.ScopeCount);
    }

    [Fact]
    public async Task The_cap_keeps_the_best_rows_not_the_first_in_the_catalogue()
    {
        var s = await BuildAsync(maxResults: 1);
        // Fit off: artist then title, so "A" leads whatever the scan order.
        var r = await s.Handler.Handle(new RankTracks("", 0));

        Assert.Equal(["A"], r.Rows.Select(x => x.Summary.Title));
    }

    [Fact]
    public void The_cap_defaults_to_250()
    {
        Assert.Equal(250, new GlanceOptions().MaxResults);
    }

    [Fact]
    public async Task Membership_is_fetched_once_for_many_ranks_and_again_after_a_track_is_added_to_a_crate()
    {
        var s = await BuildAsync();
        for (var i = 0; i < 5; i++) await s.Handler.Handle(new RankTracks("", 0) { CrateIds = [s.Peak] });
        Assert.Equal(1, s.Crates.MembershipCalls);

        s.Commands.Handle(new AddTrackToCrate(s.Ids["C"], s.Peak, "Peak", false, _from));
        var r = await s.Handler.Handle(new RankTracks("", 0) { CrateIds = [s.Peak] });

        Assert.Equal(2, s.Crates.MembershipCalls);
        Assert.Equal(["A", "B", "C"], Titles(r));
    }

    [Fact]
    public async Task Membership_cache_without_a_crate_service_is_empty_and_Invalidate_forces_a_refetch()
    {
        var empty = new CrateMembershipCache(new LibrarySessionRig().Library);
        Assert.Empty((await empty.CurrentAsync()).TracksByCrate);

        var s = await BuildAsync();
        var cache = new CrateMembershipCache(s.Rig.Library.Library);
        var first = await cache.CurrentAsync();
        Assert.Same(first, await cache.CurrentAsync());
        cache.Invalidate();
        Assert.NotSame(first, await cache.CurrentAsync());
        Assert.Equal(2, s.Crates.MembershipCalls); // the first fetch, then the refetch after Invalidate
    }

    [Fact]
    public async Task Scoping_and_ranking_run_off_the_calling_thread()
    {
        var s = await BuildAsync();
        var spy = new ThreadSpyRanker();
        var handler = new RankTracksHandler(s.Rig.Library.Library, s.Rig.Decks, spy, new GlanceScope(),
            new CrateMembershipCache(s.Rig.Library.Library), Options.Create(new GlanceOptions()));
        var caller = Environment.CurrentManagedThreadId;

        await handler.Handle(new RankTracks("", 0) { CrateIds = [s.Peak] });

        Assert.NotEqual(caller, spy.RankedOnThread);
        Assert.Equal(2, spy.RowCount);
    }

    [Fact]
    public async Task Ten_thousand_tracks_with_crate_and_tag_chips_cold_and_warm_are_timed()
    {
        const int Count = 10_000;
        var tracks = Enumerable.Range(0, Count)
            .Select(i => new Track($"/music/t{i}.mp3", $"Title {i}", $"Artist {i % 400}", TimeSpan.FromMinutes(4))).ToArray();
        var rig = new GlanceHandlerRig(tracks);
        var lib = rig.Library;
        var ids = tracks.Select(t => lib.Catalog.Assign(t.FilePath)).ToArray();
        var tags = new FakeTagService(ids.Select((id, i) => (id, i)).ToDictionary(
            x => x.id, x => (IReadOnlyList<string>)[$"tag{x.i % 20}", $"tag{x.i % 7 + 20}"]));
        await Task.Run(() => lib.Library.ScanAsync("/music", lib.Stack(tags: tags)));
        lib.Library.AttachServices(tags, lib.Crates);
        var crateIds = new List<int>();
        for (var c = 0; c < 40; c++)
        {
            var crate = await lib.Crates.CreateAsync($"Crate {c}");
            crateIds.Add(crate);
            for (var i = c; i < Count; i += 5) await lib.Crates.AddTrackAsync(crate, ids[i]);
        }
        rig.Seed(tracks.Select((t, i) => (t, i)).ToDictionary(x => x.t.FilePath, x => ($"{1 + x.i % 12}A", 100.0 + x.i % 60)));
        rig.Load(0, tracks[0], sourceBpm: 126, camelot: "8A", playing: true);
        var handler = new RankTracksHandler(lib.Library, rig.Decks,
            new GlanceRanker(new FitScorer(Options.Create(new GlanceOptions())), new GlanceQueryFactory(), new GlanceMatcher()), new GlanceScope(),
            new CrateMembershipCache(lib.Library), Options.Create(new GlanceOptions()));
        var query = new RankTracks("", 1) { CrateIds = [crateIds[0]], Tags = ["tag0"] };

        var cold = Stopwatch.StartNew();
        var first = await handler.Handle(query);
        cold.Stop();
        var warm = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            await handler.Handle(query);
            watch.Stop();
            warm.Add(watch.Elapsed.TotalMilliseconds);
        }
        var noChips = Stopwatch.StartNew();
        var all = await handler.Handle(new RankTracks("", 1));
        noChips.Stop();

        _output.WriteLine($"RankTracks 10k, 1 crate + 1 tag chip, COLD (first call incl. membership fetch + JIT): {cold.Elapsed.TotalMilliseconds:F1} ms, scope {first.ScopeCount}");
        _output.WriteLine($"RankTracks 10k, 1 crate + 1 tag chip, WARM: min {warm.Min():F1} ms, median {warm.Order().ElementAt(2):F1} ms, max {warm.Max():F1} ms");
        _output.WriteLine($"RankTracks 10k, no chips, capped to {all.Rows.Count}: {noChips.Elapsed.TotalMilliseconds:F1} ms, scope {all.ScopeCount}");
        Assert.Equal(Count, all.ScopeCount);
        Assert.Equal(250, all.Rows.Count);
        Assert.True(warm.Max() < 2000, $"warm rank took {warm.Max():F0} ms");
    }

    private sealed class ThreadSpyRanker : IGlanceRanker
    {
        public int RankedOnThread { get; private set; }
        public int RowCount { get; private set; }

        public RankedTracks Rank(IReadOnlyList<TrackSummary> rows, GlanceReference? reference, string query)
        {
            RankedOnThread = Environment.CurrentManagedThreadId;
            RowCount = rows.Count;
            return new RankedTracks([], -1, null, null, false, []);
        }
    }
}

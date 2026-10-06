using Sholto.App.Glance;
using Sholto.Data;

namespace Sholto.App.Tests.Glance;

public class GlanceRankerTests
{
    private readonly GlanceTrackBuilder _b = new();
    private readonly GlanceRanker _ranker = new(new FitScorer(), new GlanceQueryFactory(), new GlanceMatcher());

    private GlanceReference Ref() => new(0, _b.Key("8A"), 128, "/m/ref.mp3");

    private string[] Order(RankedTracks r) => r.Rows.Select(x => x.Summary.Title).ToArray();

    [Fact]
    public void Same_key_unplayed_ranks_above_unplayed_clash()
    {
        var rows = new[] { _b.Track("clash", "3B", 128), _b.Track("good", "8A", 128 * 1.01) };
        Assert.Equal(new[] { "good", "clash" }, Order(_ranker.Rank(rows, Ref(), "")));
    }

    [Fact]
    public void Played_good_ranks_below_unplayed_clash()
    {
        var rows = new[] { _b.Track("playedGood", "8A", 128, played: true), _b.Track("clash", "3B", 128) };
        RankedTracks r = _ranker.Rank(rows, Ref(), "");
        Assert.Equal(new[] { "clash", "playedGood" }, Order(r));
        Assert.Equal(FitLevel.Good, r.Rows[1].Fit);
        Assert.Equal(FitLevel.Clash, r.Rows[0].Fit);
    }

    [Fact]
    public void Reference_track_is_marked_and_sinks()
    {
        TrackSummary self = _b.Track("ref", "8A", 128) with { FilePath = "/m/ref.mp3" };
        var rows = new[] { self, _b.Track("clash", "3B", 128) };
        RankedTracks r = _ranker.Rank(rows, Ref(), "");
        Assert.Equal(new[] { "clash", "ref" }, Order(r));
        Assert.True(r.Rows[1].IsReference);
        Assert.False(r.Rows[0].IsReference);
    }

    [Fact]
    public void Equal_level_and_score_orders_by_smaller_tempo_delta()
    {
        var rows = new[] { _b.Track("far", "8A", 128 * 1.015), _b.Track("near", "8A", 128 * 1.005) };
        Assert.Equal(new[] { "near", "far" }, Order(_ranker.Rank(rows, Ref(), "")));
    }

    [Fact]
    public void Higher_score_beats_smaller_delta_within_a_level()
    {
        // 9A at 0% is score 4 (Good); 8A at +3% is score 4 (Good); 8A at 2.5%... use same level, different score.
        var rows = new[] { _b.Track("adjacentExact", "9A", 128), _b.Track("sameKeyExact", "8A", 128) };
        Assert.Equal(new[] { "sameKeyExact", "adjacentExact" }, Order(_ranker.Rank(rows, Ref(), "")));
    }

    [Fact]
    public void No_reference_turns_fit_off_and_sorts_by_artist_then_title()
    {
        var rows = new[]
        {
            _b.Track("b", "8A", 128, artist: "zed"),
            _b.Track("z", "3B", 90, artist: "Amy"),
            _b.Track("a", "8A", 128, artist: "amy"),
        };
        RankedTracks r = _ranker.Rank(rows, null, "");
        Assert.False(r.FitActive);
        Assert.Equal(-1, r.ReferenceDeck);
        Assert.Equal(new[] { "a", "z", "b" }, Order(r));
        Assert.All(r.Rows, x => Assert.Equal(FitLevel.None, x.Fit));
    }

    [Fact]
    public void Reference_facts_are_reported()
    {
        RankedTracks r = _ranker.Rank(new[] { _b.Track("a", "8A", 128) }, Ref(), "");
        Assert.True(r.FitActive);
        Assert.Equal(0, r.ReferenceDeck);
        Assert.Equal(128, r.ReferenceBpm);
        Assert.Equal(_b.Key("8A"), r.ReferenceKey);
    }

    [Fact]
    public void Query_filters_rows_and_reports_chips()
    {
        var rows = new[] { _b.Track("a", "8A", 126, tags: "techno"), _b.Track("b", "8A", 130, tags: "techno"), _b.Track("c", "8A", 126) };
        RankedTracks r = _ranker.Rank(rows, Ref(), "bpm:124-128 key:8a #techno");
        Assert.Equal(new[] { "a" }, Order(r));
        Assert.Equal(new[] { "BPM 124–128", "KEY 8A", "#techno" }, r.FilterChips);
    }

    [Fact]
    public void Empty_query_returns_every_row()
    {
        var rows = new[] { _b.Track("a", null, null), _b.Track("b", "8A", 128), _b.Track("c", "3B", 70) };
        Assert.Equal(3, _ranker.Rank(rows, Ref(), "").Rows.Count);
    }
}

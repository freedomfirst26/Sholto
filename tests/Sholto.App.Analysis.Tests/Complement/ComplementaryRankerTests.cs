using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Complement;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Harmony;

namespace Sholto.App.Analysis.Tests.Complement;

public class ComplementaryRankerTests
{
    // 8B = C major. 8A = A minor (relative), 9B = G major (close), 9A = E minor (diagonal), 3B = Db major (far).
    private static readonly Key C = new(0, true);
    private static readonly Key AMinor = new(9, false);
    private static readonly Key G = new(7, true);
    private static readonly Key EMinor = new(4, false);
    private static readonly Key Db = new(1, true);

    private readonly ComplementaryRanker _ranker = new(Options.Create(new ComplementOptions()));

    private static ComplementCandidate Section(Key key, double bpm = 124, SectionKind kind = SectionKind.Drop, float loudness = 0.85f) =>
        new(key, bpm, kind, loudness);

    private static readonly ComplementCandidate PlayingDrop = Section(C, 124, SectionKind.Drop, 0.8f);

    [Fact]
    public void An_empty_list_returns_empty()
    {
        Assert.Empty(_ranker.Rank(PlayingDrop, []));
    }

    [Fact]
    public void An_ideal_candidate_scores_one()
    {
        var ranked = _ranker.Rank(PlayingDrop, [Section(C)]);

        Assert.Equal(1.0, ranked.Single().Score, 5);
    }

    [Fact]
    public void Candidates_come_back_best_first_with_a_score_each()
    {
        var far = Section(Db);
        var diagonal = Section(EMinor);
        var ideal = Section(C);
        var close = Section(G);

        var ranked = _ranker.Rank(PlayingDrop, [far, diagonal, ideal, close]);

        Assert.Equal([ideal, close, diagonal, far], ranked.Select(r => r.Candidate));
        Assert.True(ranked[0].Score > ranked[1].Score && ranked[1].Score > ranked[2].Score && ranked[2].Score > ranked[3].Score);
    }

    [Fact]
    public void Key_fit_follows_key_compatibility()
    {
        var relative = _ranker.Score(PlayingDrop, Section(AMinor));
        var close = _ranker.Score(PlayingDrop, Section(G));
        var diagonal = _ranker.Score(PlayingDrop, Section(EMinor));

        Assert.Equal(_ranker.Score(PlayingDrop, Section(C)), relative, 9); // Perfect either way
        Assert.True(relative > close);
        Assert.True(close > diagonal);
    }

    [Fact]
    public void An_incompatible_key_scores_zero_even_when_everything_else_fits()
    {
        Assert.Equal(HarmonicMatchResult.Far, C.Compatibility(Db));
        Assert.Equal(0, _ranker.Score(PlayingDrop, Section(Db)));
    }

    [Fact]
    public void Tempo_fit_falls_off_with_the_bpm_gap()
    {
        var same = _ranker.Score(PlayingDrop, Section(C, 124));
        var near = _ranker.Score(PlayingDrop, Section(C, 126));
        var further = _ranker.Score(PlayingDrop, Section(C, 129));

        Assert.True(same > near);
        Assert.True(near > further);
        Assert.True(further > 0);
    }

    [Theory]
    [InlineData(132)] // +6.5%: beyond the default 6% window
    [InlineData(115)] // -7.3%
    [InlineData(0)]   // unknown tempo
    public void A_candidate_beyond_the_tempo_window_scores_zero(double bpm)
    {
        Assert.Equal(0, _ranker.Score(PlayingDrop, Section(C, bpm)));
    }

    [Fact]
    public void The_tempo_window_comes_from_options()
    {
        var wide = new ComplementaryRanker(Options.Create(new ComplementOptions { TempoWindowPercent = 10 }));

        Assert.True(wide.Score(PlayingDrop, Section(C, 132)) > 0);
    }

    [Theory]
    [InlineData(SectionKind.Drop, SectionKind.Drop)]
    [InlineData(SectionKind.Drop, SectionKind.Build)]
    [InlineData(SectionKind.Breakdown, SectionKind.Build)]
    public void A_contrasting_or_continuing_role_beats_an_unpaired_one(SectionKind playingKind, SectionKind pairedKind)
    {
        var playing = Section(C, kind: playingKind, loudness: 0.8f);

        var paired = _ranker.Score(playing, Section(C, kind: pairedKind));
        var unpaired = _ranker.Score(playing, Section(C, kind: SectionKind.Outro));

        Assert.True(paired > unpaired);
    }

    [Fact]
    public void A_playing_drop_ranks_drop_and_build_above_breakdown_and_intro()
    {
        var breakdown = Section(C, kind: SectionKind.Breakdown);
        var drop = Section(C, kind: SectionKind.Drop);
        var intro = Section(C, kind: SectionKind.Intro);
        var build = Section(C, kind: SectionKind.Build);

        var ranked = _ranker.Rank(PlayingDrop, [breakdown, drop, intro, build]).Select(r => r.Candidate).ToList();

        Assert.Equal([drop, build], ranked.Take(2));
    }

    [Fact]
    public void Energy_fit_prefers_similar_or_slightly_higher_loudness()
    {
        var slightlyLouder = _ranker.Score(PlayingDrop, Section(C, loudness: 0.85f));
        var same = _ranker.Score(PlayingDrop, Section(C, loudness: 0.8f));
        var muchQuieter = _ranker.Score(PlayingDrop, Section(C, loudness: 0.4f));
        var muchLouder = _ranker.Score(PlayingDrop, Section(C, loudness: 1.3f));

        Assert.True(slightlyLouder > same);
        Assert.True(same > muchQuieter);
        Assert.True(same > muchLouder);
        Assert.True(muchQuieter > 0); // energy alone never cuts a candidate out
    }

    [Fact]
    public void Ties_keep_input_order()
    {
        var first = Section(G, kind: SectionKind.Build);
        var second = Section(G, kind: SectionKind.Drop);
        var third = Section(G, kind: SectionKind.Build);

        var ranked = _ranker.Rank(PlayingDrop, [first, second, third]);

        Assert.Equal(ranked[0].Score, ranked[2].Score);
        Assert.Same(first, ranked[0].Candidate);
        Assert.Same(second, ranked[1].Candidate);
        Assert.Same(third, ranked[2].Candidate);
    }

    [Fact]
    public void Zero_scored_candidates_tie_in_input_order_at_the_end()
    {
        var far = Section(Db);
        var tooFast = Section(C, 140);
        var good = Section(C);

        var ranked = _ranker.Rank(PlayingDrop, [far, tooFast, good]);

        Assert.Same(good, ranked[0].Candidate);
        Assert.Same(far, ranked[1].Candidate);
        Assert.Same(tooFast, ranked[2].Candidate);
        Assert.Equal(0, ranked[1].Score);
        Assert.Equal(0, ranked[2].Score);
    }

    [Fact]
    public void Invalid_options_are_refused()
    {
        Assert.Throws<ArgumentException>(() => new ComplementaryRanker(Options.Create(new ComplementOptions { KeyWeight = -1 })));
        Assert.Throws<ArgumentException>(() => new ComplementaryRanker(Options.Create(new ComplementOptions
            { KeyWeight = 0, TempoWeight = 0, EnergyWeight = 0, RoleWeight = 0 })));
        Assert.Throws<ArgumentException>(() => new ComplementaryRanker(Options.Create(new ComplementOptions { TempoWindowPercent = 0 })));
        Assert.Throws<ArgumentException>(() => new ComplementaryRanker(Options.Create(new ComplementOptions { EnergyGap = 0 })));
    }
}

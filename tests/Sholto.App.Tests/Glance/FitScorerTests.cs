using Microsoft.Extensions.Options;
using Sholto.App.Glance;
using Sholto.Data;

namespace Sholto.App.Tests.Glance;

public class FitScorerTests
{
    private readonly GlanceTrackBuilder _b = new();
    private readonly FitScorer _scorer = new(Options.Create(new GlanceOptions()));

    private GlanceReference Ref() => new(0, _b.Key("8A"), 128, "/m/ref.mp3");

    private FitResult Score(string? key, double? bpm, double multiplier = 1.0) =>
        _scorer.Score(_b.Track("t", key, bpm, multiplier: multiplier), Ref());

    [Fact] public void Same_key_within_one_percent_is_good() => Assert.Equal(FitLevel.Good, Score("8A", 129.0).Level);
    // 133.12 is exactly +4%; 133.1 is +3.98% and scores tempo 1 (Good), so 133.2 (+4.06%) is the Usable example.
    [Fact] public void Same_key_just_over_four_percent_is_usable() => Assert.Equal(FitLevel.Usable, Score("8A", 133.2).Level);
    [Fact] public void Same_key_over_six_percent_is_clash() => Assert.Equal(FitLevel.Clash, Score("8A", 137).Level);
    [Fact] public void Adjacent_number_is_good_at_1_5_percent() => Assert.Equal(FitLevel.Good, Score("9A", 128 * 1.015).Level);
    [Fact] public void Adjacent_number_is_usable_at_3_percent() => Assert.Equal(FitLevel.Usable, Score("7A", 128 * 1.03).Level);
    [Fact] public void Relative_key_is_good_at_1_percent() => Assert.Equal(FitLevel.Good, Score("8B", 128 * 1.01).Level);
    [Fact] public void Two_steps_is_usable_at_1_percent() => Assert.Equal(FitLevel.Usable, Score("10A", 128 * 1.01).Level);
    [Fact] public void Diagonal_is_clash() => Assert.Equal(FitLevel.Clash, Score("9B", 128).Level);

    [Theory]
    [InlineData(64.2)]
    [InlineData(256.5)]
    public void Half_and_double_time_count(double bpm)
    {
        FitResult r = Score("8A", bpm);
        Assert.Equal(FitLevel.Good, r.Level);
        Assert.InRange(Math.Abs(r.TempoDeltaPercent!.Value), 0, 0.5);
    }

    [Fact] public void Multiplier_is_applied() => Assert.Equal(FitLevel.Good, Score("8A", 64, 2.0).Level);

    [Fact] public void Tempo_at_3_9_percent_scores_one() => Assert.Equal(4, Score("8A", 128 * 1.039).Score);
    [Fact] public void Tempo_at_4_1_percent_scores_zero() => Assert.Equal(3, Score("8A", 128 * 1.041).Score);

    [Fact]
    public void Missing_key_is_none_with_delta()
    {
        FitResult r = Score(null, 128);
        Assert.Equal(FitLevel.None, r.Level);
        Assert.Equal(0.0, r.TempoDeltaPercent!.Value, 6);
    }

    [Fact]
    public void Missing_bpm_is_none_with_null_delta()
    {
        FitResult r = Score("8A", null);
        Assert.Equal(FitLevel.None, r.Level);
        Assert.Null(r.TempoDeltaPercent);
    }

    [Fact]
    public void Analyzing_track_is_none()
    {
        TrackSummary t = _b.Track("t", "8A", 128) with { IsAnalyzing = true };
        Assert.Equal(FitLevel.None, _scorer.Score(t, Ref()).Level);
    }
}

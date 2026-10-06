using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>What a Glance row says about a track's fit, tempo and state.</summary>
public class GlanceRowTests
{
    private readonly GlanceRig _rig = new();

    private GlanceRow RowFor(TrackSummary summary, FitLevel fit, double? delta, bool reference = false)
    {
        _rig.Show(summary);
        return new GlanceRow(
            _rig.Library.RowFor(summary.FilePath)!, new RankedTrack(summary, fit, delta, reference), false);
    }

    [Theory]
    [InlineData(0.8, "+0.8%", FitLevel.Good)]
    [InlineData(-2.0, "-2.0%", FitLevel.Good)]
    [InlineData(3.5, "+3.5%", FitLevel.Usable)]
    [InlineData(-6.1, "-6.1%", FitLevel.Clash)]
    public void The_tempo_delta_is_shown_and_graded(double delta, string text, FitLevel level)
    {
        var row = RowFor(GlanceRig.Track("A"), FitLevel.Good, delta);

        Assert.Equal(text, row.TempoDeltaDisplay);
        Assert.Equal(level, row.TempoDeltaLevel);
    }

    [Fact]
    public void The_reference_shows_no_fit_and_no_delta_and_is_playing_now()
    {
        var row = RowFor(GlanceRig.Track("A", "Zed"), FitLevel.Good, 0, reference: true);

        Assert.Equal(FitLevel.None, row.FitLevel);
        Assert.Equal("", row.TempoDeltaDisplay);
        Assert.Equal("Zed · playing now", row.Subtitle);
        Assert.True(row.IsFaded);
    }

    [Fact]
    public void The_subtitle_names_played_and_analysing_tracks()
    {
        var played = RowFor(GlanceRig.Track("A", "Zed") with { IsPlayed = true }, FitLevel.None, null);
        var analysing = RowFor(GlanceRig.Track("B", "Zed") with { IsAnalyzing = true }, FitLevel.None, null);

        Assert.Equal("Zed · played", played.Subtitle);
        Assert.Equal("Zed · analysing", analysing.Subtitle);
    }
}

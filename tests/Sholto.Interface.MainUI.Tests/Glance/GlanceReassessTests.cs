using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The slow indicator: on after 150 ms of a ranking the person asked for, held on for at least
/// 300 ms, never started by a ranking the person did not ask for.</summary>
public class GlanceReassessTests
{
    private readonly GlanceRig _rig = new();
    private GlanceViewModel Glance => _rig.Glance;

    private static readonly TrackSummary Alpha = GlanceRig.Track("Alpha");
    private static readonly TrackSummary Bravo = GlanceRig.Track("Bravo");

    public GlanceReassessTests()
    {
        _rig.Show(Alpha, Bravo);
        Glance.Open();
    }

    /// <summary>Move the clock by <paramref name="ms"/> and run a frame.</summary>
    private void Frame(double ms)
    {
        _rig.Clock.Advance(ms / 1000);
        Glance.OnFrame(_rig.Clock.Now);
    }

    private TaskCompletionSource<RankedTracks> Gate()
    {
        var gate = new TaskCompletionSource<RankedTracks>();
        _rig.Answer = _ => gate.Task;
        return gate;
    }

    [Fact]
    public void A_slow_typed_ranking_shows_the_indicator_after_150_ms_and_not_before()
    {
        Gate();
        Glance.Query = "b";

        Frame(149);
        Assert.False(Glance.IsReassessing);

        Frame(2);
        Assert.True(Glance.IsReassessing);
    }

    [Fact]
    public void A_result_after_the_indicator_is_held_until_it_has_shown_300_ms()
    {
        var gate = Gate();
        Glance.Query = "b";
        Frame(151);
        Assert.True(Glance.IsReassessing);
        Frame(49);

        gate.SetResult(GlanceRig.Ranked(0, [Bravo]));
        Assert.Equal(["Alpha", "Bravo"], Glance.Rows.Select(r => r.Row.Title));
        Assert.True(Glance.IsReassessing);

        Frame(250);
        Assert.Equal(["Alpha", "Bravo"], Glance.Rows.Select(r => r.Row.Title));
        Assert.True(Glance.IsReassessing);

        Frame(2);
        Assert.Equal(["Bravo"], Glance.Rows.Select(r => r.Row.Title));
        Assert.False(Glance.IsReassessing);
    }

    [Fact]
    public void A_result_before_150_ms_never_shows_the_indicator_and_applies_at_once()
    {
        var gate = Gate();
        var seen = false;
        Glance.PropertyChanged += (_, e) => seen |= e.PropertyName == nameof(Glance.IsReassessing);
        Glance.Query = "b";
        Frame(100);

        gate.SetResult(GlanceRig.Ranked(0, [Bravo]));
        Frame(500);

        Assert.Equal(["Bravo"], Glance.Rows.Select(r => r.Row.Title));
        Assert.False(Glance.IsReassessing);
        Assert.False(seen);
    }

    [Fact]
    public void A_slow_ranking_the_person_did_not_ask_for_never_shows_the_indicator()
    {
        var gate = Gate();
        _rig.Bus.Publish(new TrackSummaryChanged(Alpha with { Bpm = 100 }));
        Frame(16);
        Assert.Equal(2, _rig.Ranker.Asked.Count);

        Frame(1000);
        Assert.False(Glance.IsReassessing);

        gate.SetResult(GlanceRig.Ranked(0, [Bravo]));
        Assert.Equal(["Bravo"], Glance.Rows.Select(r => r.Row.Title));
        Assert.False(Glance.IsReassessing);
    }

    [Fact]
    public void Only_the_newest_result_applies_through_the_hold()
    {
        var first = new TaskCompletionSource<RankedTracks>();
        var second = new TaskCompletionSource<RankedTracks>();
        var pending = new Queue<TaskCompletionSource<RankedTracks>>([first, second]);
        _rig.Answer = _ => pending.Dequeue().Task;
        Glance.Query = "b";
        Frame(151);
        Glance.Query = "bs";

        first.SetResult(GlanceRig.Ranked(0, [Alpha]));
        Frame(400);
        Assert.Equal(["Alpha", "Bravo"], Glance.Rows.Select(r => r.Row.Title));

        second.SetResult(GlanceRig.Ranked(0, [Bravo]));

        Assert.Equal(["Bravo"], Glance.Rows.Select(r => r.Row.Title));
        Assert.False(Glance.IsReassessing);
    }

    [Fact]
    public void Closing_clears_the_indicator()
    {
        Gate();
        Glance.Query = "b";
        Frame(151);

        Glance.Close();

        Assert.False(Glance.IsReassessing);
    }
}

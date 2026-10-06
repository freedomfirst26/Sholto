using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.Tests.CollapseToIcon;

/// <summary>Two consumers of the same component, on the same clock, do not touch each other.</summary>
public class TwoConsumersTests
{
    private readonly FakeFrameClock _clock = new();
    private readonly CountingHintPolicy _policyA = new(true);
    private readonly CountingHintPolicy _policyB = new(false);
    private readonly CollapseToIconSequence _a;
    private readonly CollapseToIconSequence _b;

    public TwoConsumersTests()
    {
        var motion = new FixedMotionPreference(false);
        var timingsA = new CollapseToIconTimings(
            TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), 2,
            TimeSpan.FromMilliseconds(80), TimeSpan.FromSeconds(2));
        var timingsB = new CollapseToIconTimings(
            TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300), 1,
            TimeSpan.FromMilliseconds(80), TimeSpan.FromSeconds(2));
        _a = new CollapseToIconSequence(_clock, motion, new CollapseToIconOptions("a", timingsA), _policyA);
        _b = new CollapseToIconSequence(_clock, motion, new CollapseToIconOptions("b", timingsB), _policyB);
    }

    private void Tick(double seconds)
    {
        _clock.Advance(seconds);
        _a.OnFrame(_clock.Now);
        _b.OnFrame(_clock.Now);
    }

    [Fact]
    public void Collapsing_one_leaves_the_other_open()
    {
        _a.Open();
        _b.Open();

        _a.Collapse();
        Tick(0.1);

        Assert.Equal(CollapseToIconState.Collapsing, _a.State);
        Assert.Equal(CollapseToIconState.Open, _b.State);
    }

    [Fact]
    public void Each_runs_on_its_own_timings()
    {
        _a.Open();
        _b.Open();
        _a.Collapse();
        _b.Collapse();

        Tick(0.25);

        Assert.Equal(CollapseToIconState.Hinting, _a.State);
        Assert.Equal(CollapseToIconState.Collapsing, _b.State);
    }

    [Fact]
    public void Engaging_one_target_does_not_end_the_others_hint()
    {
        var hintsBoth = new CountingHintPolicy(true);
        var motion = new FixedMotionPreference(false);
        var timings = new CollapseToIconTimings(
            TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), 2,
            TimeSpan.FromMilliseconds(80), TimeSpan.FromSeconds(2));
        var first = new CollapseToIconSequence(_clock, motion, new CollapseToIconOptions("first", timings), hintsBoth);
        var second = new CollapseToIconSequence(_clock, motion, new CollapseToIconOptions("second", timings), hintsBoth);
        first.Open();
        second.Open();
        first.Collapse();
        second.Collapse();
        _clock.Advance(0.15);
        first.OnFrame(_clock.Now);
        second.OnFrame(_clock.Now);

        first.TargetEngaged();

        Assert.Equal(CollapseToIconState.Idle, first.State);
        Assert.Equal(CollapseToIconState.Hinting, second.State);
    }

    [Fact]
    public void Each_asks_only_its_own_policy()
    {
        _a.Open();
        _b.Open();
        _a.Collapse();
        _b.Collapse();

        Tick(0.5);

        Assert.Equal(1, _policyA.Asked);
        Assert.Equal(1, _policyB.Asked);
        Assert.Equal(1, _policyA.Shown);
        Assert.Equal(0, _policyB.Shown);
        Assert.Equal(CollapseToIconState.Idle, _b.State);
    }
}

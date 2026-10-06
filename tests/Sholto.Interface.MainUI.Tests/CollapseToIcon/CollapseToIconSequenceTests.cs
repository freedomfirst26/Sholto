using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.Tests.CollapseToIcon;

/// <summary>The collapse-to-icon state machine, driven by calling OnFrame with times a test chooses.</summary>
public class CollapseToIconSequenceTests
{
    // Deliberately not the standard timings, so a pass proves they come from the options.
    private static readonly CollapseToIconTimings Timings = new(
        TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(500), 2,
        TimeSpan.FromMilliseconds(80), TimeSpan.FromSeconds(2));

    private readonly FakeFrameClock _clock = new();

    private CollapseToIconSequence Build(IHintPolicy policy, bool reduced = false) =>
        new(_clock, new FixedMotionPreference(reduced), new CollapseToIconOptions("test", Timings), policy);

    private CollapseToIconSequence CollapsedFromOpen(IHintPolicy policy, bool reduced = false)
    {
        var sequence = Build(policy, reduced);
        sequence.Open();
        sequence.Collapse();
        return sequence;
    }

    private void Tick(CollapseToIconSequence sequence, double afterSeconds)
    {
        _clock.Advance(afterSeconds);
        sequence.OnFrame(_clock.Now);
    }

    [Fact]
    public void Starts_idle_and_hidden()
    {
        var sequence = Build(new AlwaysHintPolicy());

        Assert.Equal(CollapseToIconState.Idle, sequence.State);
        Assert.False(sequence.IsShown);
    }

    [Fact]
    public void Collapse_runs_open_collapsing_hinting_idle_on_the_option_timings()
    {
        var sequence = CollapsedFromOpen(new AlwaysHintPolicy());
        Assert.Equal(CollapseToIconState.Collapsing, sequence.State);
        Assert.True(sequence.IsShown);

        Tick(sequence, 0.19);
        Assert.Equal(CollapseToIconState.Collapsing, sequence.State);

        Tick(sequence, 0.02);
        Assert.Equal(CollapseToIconState.Hinting, sequence.State);
        Assert.False(sequence.IsShown);
        Assert.False(sequence.IsHintStatic);

        Tick(sequence, 0.9);
        Assert.Equal(CollapseToIconState.Hinting, sequence.State);

        Tick(sequence, 0.2);
        Assert.Equal(CollapseToIconState.Idle, sequence.State);
    }

    [Fact]
    public void Target_engaged_while_hinting_ends_the_hint()
    {
        var sequence = CollapsedFromOpen(new AlwaysHintPolicy());
        Tick(sequence, 0.25);

        sequence.TargetEngaged();

        Assert.Equal(CollapseToIconState.Idle, sequence.State);
    }

    [Fact]
    public void Target_engaged_outside_a_hint_does_nothing()
    {
        var sequence = CollapsedFromOpen(new AlwaysHintPolicy());

        sequence.TargetEngaged();

        Assert.Equal(CollapseToIconState.Collapsing, sequence.State);
    }

    [Fact]
    public void Open_part_way_through_collapsing_returns_to_open_and_never_hints()
    {
        var policy = new CountingHintPolicy(true);
        var sequence = CollapsedFromOpen(policy);
        Tick(sequence, 0.12);

        sequence.Open();
        Tick(sequence, 5);

        Assert.Equal(CollapseToIconState.Open, sequence.State);
        Assert.Equal(0, policy.Asked);
        Assert.Equal(0, policy.Shown);
    }

    [Fact]
    public void Open_while_hinting_returns_to_open()
    {
        var sequence = CollapsedFromOpen(new AlwaysHintPolicy());
        Tick(sequence, 0.25);

        sequence.Open();

        Assert.Equal(CollapseToIconState.Open, sequence.State);
    }

    [Fact]
    public void Collapse_when_idle_is_a_no_op()
    {
        var sequence = Build(new AlwaysHintPolicy());
        var changes = 0;
        sequence.Changed += () => changes++;

        sequence.Collapse();

        Assert.Equal(CollapseToIconState.Idle, sequence.State);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void A_never_hint_policy_goes_straight_from_collapsing_to_idle()
    {
        var sequence = CollapsedFromOpen(new NeverHintPolicy());

        Tick(sequence, 0.25);

        Assert.Equal(CollapseToIconState.Idle, sequence.State);
    }

    [Fact]
    public void Hint_shown_is_reported_once_per_hint()
    {
        var policy = new CountingHintPolicy(true);
        var sequence = CollapsedFromOpen(policy);

        for (var i = 0; i < 40; i++) Tick(sequence, 0.05);

        Assert.Equal(CollapseToIconState.Idle, sequence.State);
        Assert.Equal(1, policy.Shown);

        sequence.Open();
        sequence.Collapse();
        for (var i = 0; i < 40; i++) Tick(sequence, 0.05);
        Assert.Equal(2, policy.Shown);
    }

    [Fact]
    public void Reduced_motion_collapses_over_the_fade_and_holds_a_still_hint()
    {
        var sequence = CollapsedFromOpen(new AlwaysHintPolicy(), reduced: true);
        Assert.True(sequence.Reduced);

        Tick(sequence, 0.07);
        Assert.Equal(CollapseToIconState.Collapsing, sequence.State);

        Tick(sequence, 0.02);
        Assert.Equal(CollapseToIconState.Hinting, sequence.State);
        Assert.True(sequence.IsHintStatic);

        Tick(sequence, 1.9);
        Assert.Equal(CollapseToIconState.Hinting, sequence.State);

        Tick(sequence, 0.2);
        Assert.Equal(CollapseToIconState.Idle, sequence.State);
        Assert.False(sequence.IsHintStatic);
    }

    [Fact]
    public void A_stalled_frame_lands_in_the_right_state_and_still_reports_the_hint()
    {
        var policy = new CountingHintPolicy(true);
        var sequence = CollapsedFromOpen(policy);

        Tick(sequence, 0.5);
        Assert.Equal(CollapseToIconState.Hinting, sequence.State);
        Assert.Equal(1, policy.Shown);

        var stalled = CollapsedFromOpen(policy);
        Tick(stalled, 5);
        Assert.Equal(CollapseToIconState.Idle, stalled.State);
        Assert.Equal(2, policy.Shown);
    }

    [Fact]
    public void Changed_fires_once_per_transition_not_per_tick()
    {
        var sequence = Build(new AlwaysHintPolicy());
        var changes = 0;
        sequence.Changed += () => changes++;

        sequence.Open();
        Assert.Equal(1, changes);
        sequence.Open();
        Assert.Equal(1, changes);
        sequence.Collapse();
        for (var i = 0; i < 10; i++) Tick(sequence, 0.01);
        Assert.Equal(2, changes);
        Tick(sequence, 0.2);
        Assert.Equal(3, changes);
        for (var i = 0; i < 10; i++) Tick(sequence, 0.01);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void A_tick_allocates_nothing_in_any_state()
    {
        var sequence = Build(new AlwaysHintPolicy());
        var now = _clock.Now;

        sequence.OnFrame(now);   // warm up
        Assert.Equal(0, Allocated(sequence, now));            // idle
        sequence.Open();
        Assert.Equal(0, Allocated(sequence, now));            // open
        sequence.Collapse();
        Assert.Equal(0, Allocated(sequence, now));            // collapsing
        _clock.Advance(0.25);
        sequence.OnFrame(_clock.Now);
        Assert.Equal(CollapseToIconState.Hinting, sequence.State);
        Assert.Equal(0, Allocated(sequence, _clock.Now));     // hinting
    }

    private static long Allocated(CollapseToIconSequence sequence, DateTime now)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) sequence.OnFrame(now);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

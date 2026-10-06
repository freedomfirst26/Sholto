using Sholto.App.Performance;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>The backspin time and distance reach the platter physics through <see cref="ScratchEngine"/>: a
/// released fling coasts for the backspin time and covers the backspin distance (beats at 120 BPM with no
/// analysis, so 0.5 s each), whatever the other knob says. The fling is seeded on the engine's public per-deck
/// state the way a hand leaving a spinning platter leaves it, then the engine is ticked at 60 Hz and the rate
/// it pushes (<see cref="ScratchState.Velocity"/>) is summed into a distance.</summary>
public class ScratchEngineBackspinFeelTests
{
    private const double Frame = 1.0 / 60.0;
    private const double BeatSec = 0.5;
    private static readonly Origin Settings = new(InterfaceIds.MainUI, "settings", "knob");

    private sealed record Coast(int Frames, double Distance, double FirstRate, bool NeverSpeedsUp, double MaxAbsRate);

    private static PerformanceRig RigWith(double seconds, double beats)
    {
        var rig = new PerformanceRig();
        rig.Core.BackspinFeel.Handle(new SetBackspinTime(seconds, Settings));
        rig.Core.BackspinFeel.Handle(new SetBackspinDistance(beats, Settings));
        return rig;
    }

    private static void Seed(ScratchState st, double peak, bool wasPlaying, DateTime now)
    {
        st.Active = true;
        st.Touching = false;
        st.WasPlaying = wasPlaying;
        st.Coasting = false;
        st.Timed = false;
        st.Velocity = peak;
        st.PeakVelocity = peak;
        st.LastTickAt = DateTime.MinValue;
        st.LastFlushAt = now;
    }

    private static Coast Run(double seconds, double beats, double peak, bool wasPlaying = false)
    {
        var rig = RigWith(seconds, beats);
        var st = rig.Scratch.StateOf(0);
        var rest = wasPlaying && peak > 0 ? rig.Core.Decks.DeckFor(0).Tempo.PlaybackSpeed : 0.0;
        var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        Seed(st, peak, wasPlaying, now);

        int frames = 0;
        double distance = 0, first = 0, previous = double.MaxValue, maxAbs = 0;
        bool falling = true;
        while (st.Active)
        {
            if (++frames > 60 * 60) throw new InvalidOperationException("the fling never came to rest");
            now = now.AddSeconds(Frame);
            rig.Scratch.Tick(0, now);
            if (!st.Active) break;
            var excess = Math.Abs(st.Velocity - rest);
            if (frames == 1) first = st.Velocity;
            if (excess > previous + 1e-9) falling = false;
            previous = excess;
            maxAbs = Math.Max(maxAbs, Math.Abs(st.Velocity));
            distance += (st.Velocity - rest) * Frame;
        }
        return new Coast(frames, distance, first, falling, maxAbs);
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(0.6)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void The_coast_lasts_the_backspin_time_within_a_frame(double seconds)
    {
        var coast = Run(seconds, 2, -10);
        Assert.InRange((coast.Frames - 1) * Frame, seconds - 1e-9, seconds + Frame + 1e-9);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void The_coast_covers_the_backspin_distance_in_beats(double beats)
    {
        var coast = Run(0.6, beats, -10);
        Assert.InRange(-coast.Distance, beats * BeatSec * 0.99, beats * BeatSec * 1.01);
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void Changing_the_time_leaves_the_distance_alone(double seconds)
    {
        var coast = Run(seconds, 2, -10);
        Assert.InRange(-coast.Distance, 2 * BeatSec * 0.99, 2 * BeatSec * 1.01);
    }

    [Fact]
    public void Changing_the_distance_leaves_the_time_alone()
    {
        var one = Run(0.6, 1, -10);
        var eight = Run(0.6, 8, -10);
        Assert.Equal(one.Frames, eight.Frames);
    }

    [Fact]
    public void A_harder_fling_goes_further_in_the_same_time()
    {
        var soft = Run(0.6, 2, -5);
        var firm = Run(0.6, 2, -10);
        var hard = Run(0.6, 2, -20);
        var brutal = Run(0.6, 2, -40);
        Assert.True(-soft.Distance < -firm.Distance && -firm.Distance < -hard.Distance);
        Assert.Equal(soft.Frames, firm.Frames);
        Assert.Equal(firm.Frames, hard.Frames);
        Assert.Equal(firm.Frames, brutal.Frames);
        Assert.InRange(-hard.Distance / -firm.Distance, Math.Sqrt(2) * 0.99, Math.Sqrt(2) * 1.01);
        Assert.InRange(-brutal.Distance / -firm.Distance, 1.98, 2.02);   // strength capped at 2
    }

    [Theory]
    [InlineData(0.0, 2.0)]
    [InlineData(0.6, 0.0)]
    public void Zero_time_or_zero_distance_stops_dead_on_the_first_frame(double seconds, double beats)
    {
        var coast = Run(seconds, beats, -20);
        Assert.Equal(1, coast.Frames);
    }

    [Fact]
    public void The_coast_is_fastest_on_the_first_frame_and_only_slows_down()
    {
        var coast = Run(0.6, 2, -10);
        Assert.True(coast.NeverSpeedsUp);
        Assert.Equal(Math.Abs(coast.FirstRate), coast.MaxAbsRate, 6);
        Assert.True(Math.Abs(coast.FirstRate) > 3.0);
    }

    [Fact]
    public void A_forward_fling_on_a_playing_deck_rests_at_playback_speed_and_covers_the_extra_distance()
    {
        var rig = RigWith(0.6, 2);
        var st = rig.Scratch.StateOf(0);
        var rest = rig.Core.Decks.DeckFor(0).Tempo.PlaybackSpeed;
        var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        Seed(st, 10, true, now);
        double extra = 0, last = 0;
        while (st.Active)
        {
            now = now.AddSeconds(Frame);
            rig.Scratch.Tick(0, now);
            if (!st.Active) break;
            extra += (st.Velocity - rest) * Frame;
            last = st.Velocity;
        }
        Assert.InRange(extra, 2 * BeatSec * 0.99, 2 * BeatSec * 1.01);
        Assert.InRange(last, rest, rest + 0.5);
    }

    [Fact]
    public void A_launch_is_capped_at_the_max_launch_rate()
    {
        var coast = Run(0.1, 16, -10);
        Assert.True(coast.MaxAbsRate <= new ScratchOptions().MaxLaunchRate + 1e-9);
        Assert.True(-coast.Distance < 16 * BeatSec * 0.99);   // the cap bites: short of the target
    }

    [Fact]
    public void A_hand_landing_mid_coast_clears_the_timed_coast()
    {
        var rig = RigWith(0.6, 2);
        var st = rig.Scratch.StateOf(0);
        var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        Seed(st, -10, false, now);
        for (var i = 0; i < 5; i++) { now = now.AddSeconds(Frame); rig.Scratch.Tick(0, now); }
        Assert.True(st.Timed);
        st.Touching = true;
        now = now.AddSeconds(Frame);
        rig.Scratch.Tick(0, now);
        Assert.False(st.Timed);
    }

    [Fact]
    public void A_real_time_coast_allocates_nothing_after_warm_up()
    {
        var rig = RigWith(0.6, 2);
        var st = rig.Scratch.StateOf(0);
        var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 2_000; i++)
        {
            Seed(st, -10, false, now);
            now = now.AddSeconds(Frame);
            rig.Scratch.Tick(0, now);
        }

        Seed(st, -10, false, now);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            if (!st.Active) Seed(st, -10, false, now);
            now = now.AddSeconds(Frame);
            rig.Scratch.Tick(0, now);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}

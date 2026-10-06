using Microsoft.Extensions.Options;
using Sholto.App;
using Sholto.App.Decks;
using Sholto.App.Performance;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>The vinyl brake (pause on a scratch-capable deck) is unchanged by the timed coast, and a pause
/// pressed in the middle of a timed fling hands the coast over to it. Pause presses are raised on the engine
/// through a stand-in <see cref="IPlaybackRequests"/>.</summary>
public class ScratchEngineBrakeTests
{
    private const double Frame = 1.0 / 60.0;

    private sealed class Requests : IPlaybackRequests
    {
        public event Action<int>? BrakePauseRequested;
        public void OnPlayPressed(int deck) => BrakePauseRequested?.Invoke(deck);
    }

    private readonly Requests _requests = new();
    private readonly PerformanceRig _rig = new();
    private readonly ScratchEngine _engine;
    private DateTime _now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    public ScratchEngineBrakeTests()
    {
        _engine = new ScratchEngine(_rig.Core.Decks, _requests, _rig.Recency, _rig.Clock,
            Options.Create(new ScratchOptions()), _rig.Core.BackspinFeel);
    }

    private int TickToRest(ScratchState st)
    {
        for (var frame = 1; frame <= 60 * 60; frame++)
        {
            _now = _now.AddSeconds(Frame);
            _engine.Tick(0, _now);
            if (!st.Active) return frame;
        }
        throw new InvalidOperationException("the deck never came to rest");
    }

    [Fact]
    public void A_pause_on_a_playing_deck_is_a_brake_from_playback_speed_that_pauses_at_rest()
    {
        var st = _engine.StateOf(0);
        _requests.OnPlayPressed(0);

        Assert.True(st.Active);
        Assert.True(st.PauseAtEnd);
        Assert.False(st.Timed);
        Assert.Equal(_rig.Core.Decks.DeckFor(0).Tempo.PlaybackSpeed, st.Velocity);

        var frames = TickToRest(st);
        // Unity is inside the 2.0 knee, so the brake is the exponential tail: ln(1/0.02) × 0.175 s ≈ 0.68 s.
        Assert.InRange(frames, 36, 46);
        Assert.False(st.PauseAtEnd);
    }

    [Fact]
    public void A_pause_mid_fling_hands_over_to_the_brake_and_the_deck_still_parks()
    {
        _rig.Core.BackspinFeel.Handle(new SetBackspinTime(2.0, new Origin(InterfaceIds.MainUI, "settings", "knob")));
        var st = _engine.StateOf(0);
        st.Active = true;
        st.WasPlaying = false;
        st.Velocity = -10;
        st.PeakVelocity = -10;
        st.LastFlushAt = _now;
        for (var i = 0; i < 6; i++) { _now = _now.AddSeconds(Frame); _engine.Tick(0, _now); }
        Assert.True(st.Timed);
        var rateAtPress = st.Velocity;

        _requests.OnPlayPressed(0);

        Assert.False(st.Timed);
        Assert.True(st.PauseAtEnd);
        Assert.False(st.WasPlaying);
        var frames = TickToRest(st);
        // The timed coast had 1.9 s (114 frames) left; the brake from the current rate is far sooner.
        Assert.True(frames < 114, $"brake took {frames} frames from rate {rateAtPress:F2}");
        Assert.False(st.PauseAtEnd);
    }
}

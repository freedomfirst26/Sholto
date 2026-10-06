using Sholto.App.Decks;
using Sholto.Data;
using Sholto.TestSupport;

namespace Sholto.App.Tests;

/// <summary>A deck's transport phase and end-of-track flash, over a settable clock and a real bus.</summary>
public class DeckPlayPhaseTests
{
    private readonly SettableFrameClock _clock = new();
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly DeckPlayPhase _phase;
    private readonly List<DeckChange> _changes = [];

    public DeckPlayPhaseTests()
    {
        _phase = new DeckPlayPhase(0, _clock, _bus);
        _phase.Changed += _changes.Add;
    }

    [Fact]
    public void A_deck_that_is_not_playing_is_stopped_whatever_the_position()
    {
        _phase.PlayPosition = 0.95;

        Assert.Equal(PlayPhase.Stopped, _phase.PlayState);
    }

    [Fact]
    public void Playing_is_ending_from_ninety_percent_and_only_while_playing()
    {
        _phase.IsPlaying = true;
        _phase.PlayPosition = 0.89;
        Assert.Equal(PlayPhase.Playing, _phase.PlayState);

        _phase.PlayPosition = 0.9;
        Assert.Equal(PlayPhase.Ending, _phase.PlayState);

        _phase.IsPlaying = false;
        Assert.Equal(PlayPhase.Stopped, _phase.PlayState);
    }

    [Fact]
    public void The_flash_is_lit_for_400_ms_then_dark_for_400_ms()
    {
        _phase.IsPlaying = true;
        var start = _clock.Now;
        _phase.PlayPosition = 0.95;
        Assert.True(_phase.EndFlashOn);

        _clock.Now = start.AddMilliseconds(399);
        _phase.UpdateFlash();
        Assert.True(_phase.EndFlashOn);

        _clock.Now = start.AddMilliseconds(400);
        _phase.UpdateFlash();
        Assert.False(_phase.EndFlashOn);

        _clock.Now = start.AddMilliseconds(800);
        _phase.UpdateFlash();
        Assert.True(_phase.EndFlashOn);
    }

    [Fact]
    public void The_flash_does_not_run_outside_the_ending_phase()
    {
        _phase.IsPlaying = true;
        _phase.PlayPosition = 0.5;
        _changes.Clear();

        _clock.Now = _clock.Now.AddMilliseconds(500);
        _phase.UpdateFlash();

        Assert.Empty(_changes);
        Assert.False(_phase.EndFlashOn);
    }

    [Fact]
    public void Starting_play_raises_IsPlaying_then_PlayState()
    {
        _phase.IsPlaying = true;

        Assert.Equal([DeckChange.IsPlaying, DeckChange.PlayState], _changes);
    }

    [Fact]
    public void Publishing_the_current_phase_announces_stopped_with_the_flash_off()
    {
        var plays = new RecordingHandler<DeckPlayStateChanged>();

        _phase.PublishCurrent();
        using var sub = _bus.Subscribe(plays);

        Assert.Equal([new DeckPlayStateChanged(0, PlayPhase.Stopped, false)], plays.Received);
    }

    [Fact]
    public void The_per_frame_position_and_flash_update_allocates_nothing_after_warm_up()
    {
        _phase.Changed -= _changes.Add;   // the recording list would grow and count as an allocation
        _phase.IsPlaying = true;
        var start = _clock.Now;
        _phase.PlayPosition = 0.95;
        for (var i = 0; i < 200; i++)
        {
            _clock.Now = start.AddMilliseconds(i * 16);
            _phase.PlayPosition = 0.95;
            _phase.UpdateFlash();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 200; i < 2200; i++)
        {
            _clock.Now = start.AddMilliseconds(i * 16);
            _phase.PlayPosition = 0.95;
            _phase.UpdateFlash();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}

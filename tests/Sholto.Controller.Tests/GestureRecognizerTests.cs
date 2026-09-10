using Sholto.Controller;
using Sholto.Controller.Gestures;
using Xunit;

namespace Sholto.Controller.Tests;

public class GestureRecognizerTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static (GestureRecognizer r, Func<ControllerEvent, Gesture?> go) New()
    {
        var r = new GestureRecognizer();
        return (r, evt => r.Recognize(evt, T0));
    }

    [Fact]
    public void Play_press_is_a_per_deck_gesture()
    {
        var (_, go) = New();
        var g = go(new ControllerEvent.PlayPressed(1));
        Assert.NotNull(g);
        Assert.Equal(GestureIds.PlayPress, g!.Id);
        Assert.Equal(1, g.Deck);
    }

    [Fact]
    public void Shift_is_tracked_and_reported_as_its_own_gesture()
    {
        var (r, go) = New();
        var g = go(new ControllerEvent.DeckShift(0, Pressed: true));
        Assert.Equal(GestureIds.ShiftHold, g!.Id);
        Assert.True(r.IsShiftHeld(0));
        Assert.False(r.IsShiftHeld(1));

        go(new ControllerEvent.DeckShift(0, Pressed: false));
        Assert.False(r.IsShiftHeld(0));
    }

    [Fact]
    public void Transport_cue_is_plain_or_restart_depending_on_the_wire_flag()
    {
        var (_, go) = New();
        Assert.Equal(GestureIds.CueTransportPlain,
            go(new ControllerEvent.TransportCuePressed(0, Shifted: false))!.Id);
        Assert.Equal(GestureIds.CueTransportRestart,
            go(new ControllerEvent.TransportCuePressed(0, Shifted: true))!.Id);
    }

    [Fact]
    public void Transport_cue_also_restarts_when_shift_is_held_but_the_wire_says_plain()
    {
        // Fallback for a firmware that sends plain CUE with Shift held, matching
        // the existing Orchestrator behaviour we are replacing.
        var (_, go) = New();
        go(new ControllerEvent.DeckShift(0, Pressed: true));
        Assert.Equal(GestureIds.CueTransportRestart,
            go(new ControllerEvent.TransportCuePressed(0, Shifted: false))!.Id);
    }

    [Fact]
    public void Shift_on_one_deck_does_not_change_the_other_decks_cue()
    {
        var (_, go) = New();
        go(new ControllerEvent.DeckShift(0, Pressed: true));
        Assert.Equal(GestureIds.CueTransportPlain,
            go(new ControllerEvent.TransportCuePressed(1, Shifted: false))!.Id);
    }

    [Theory]
    [InlineData(0, GestureIds.PadStemDrums)]
    [InlineData(1, GestureIds.PadStemVocals)]
    [InlineData(2, GestureIds.PadStemInstrumental)]
    public void Stem_pads_map_to_one_gesture_each(int group, string expected)
    {
        var (_, go) = New();
        Assert.Equal(expected, go(new ControllerEvent.StemToggle(0, group))!.Id);
    }

    [Fact]
    public void Pad_page_selection_is_tracked()
    {
        var (r, go) = New();
        Assert.Equal(GestureIds.PadModePadFx1,
            go(new ControllerEvent.PadPageSelected(1, PadPage.PadFx1))!.Id);
        Assert.Equal(PadPage.PadFx1, r.PageFor(1));
        Assert.Equal(PadPage.HotCue, r.PageFor(0));
    }

    [Fact]
    public void Grid_nudge_direction_comes_from_the_sign_and_stays_deckless()
    {
        var (_, go) = New();
        var back = go(new ControllerEvent.NudgeGrid(-1, -1))!;
        Assert.Equal(GestureIds.GridNudgeBack, back.Id);
        Assert.Equal(-1, back.Deck);
        Assert.Equal(GestureIds.GridNudgeForward, go(new ControllerEvent.NudgeGrid(-1, +1))!.Id);
    }

    [Fact]
    public void Raw_events_the_controller_consumes_produce_no_gesture()
    {
        var (_, go) = New();
        Assert.Null(go(new ControllerEvent.CueToggle(0)));
        Assert.Null(go(new ControllerEvent.MasterCuePressed()));
    }

    [Fact]
    public void The_source_event_travels_with_the_gesture()
    {
        var (_, go) = New();
        var evt = new ControllerEvent.PlayPressed(1);
        Assert.Same(evt, go(evt)!.Source);
    }

    [Fact]
    public void Eq_is_a_plain_turn_until_the_stem_level_button_is_held()
    {
        var (_, go) = New();
        Assert.Equal(GestureIds.EqTurn,
            go(new ControllerEvent.EqMoved(0, EqBand.High, 0.7))!.Id);

        go(new ControllerEvent.StemLevelMode(Pressed: true));
        Assert.Equal(GestureIds.EqStemLevelTurn,
            go(new ControllerEvent.EqMoved(0, EqBand.High, 0.7))!.Id);

        go(new ControllerEvent.StemLevelMode(Pressed: false));
        Assert.Equal(GestureIds.EqTurn,
            go(new ControllerEvent.EqMoved(0, EqBand.High, 0.7))!.Id);
    }

    [Fact]
    public void The_stem_level_modifier_applies_to_both_decks()
    {
        var (_, go) = New();
        go(new ControllerEvent.StemLevelMode(Pressed: true));
        Assert.Equal(GestureIds.EqStemLevelTurn,
            go(new ControllerEvent.EqMoved(1, EqBand.Low, 0.2))!.Id);
    }

    [Fact]
    public void Global_controls_are_deckless()
    {
        var (_, go) = New();
        Assert.Equal(-1, go(new ControllerEvent.CrossfaderMoved(0.5))!.Deck);
        Assert.Equal(-1, go(new ControllerEvent.BrowseRotated(3))!.Deck);
    }

    [Fact]
    public void Faders_and_knobs_map_to_their_own_gestures()
    {
        var (_, go) = New();
        Assert.Equal(GestureIds.VolumeMove, go(new ControllerEvent.ChannelVolumeMoved(0, 0.8))!.Id);
        Assert.Equal(GestureIds.TempoMove,  go(new ControllerEvent.TempoMoved(1, 0.5))!.Id);
        Assert.Equal(GestureIds.FilterTurn, go(new ControllerEvent.FilterMoved(0, 0.3))!.Id);
        Assert.Equal(GestureIds.CrossfaderMove, go(new ControllerEvent.CrossfaderMoved(0.1))!.Id);
        Assert.Equal(GestureIds.BrowseTurn, go(new ControllerEvent.BrowseRotated(-2))!.Id);
    }

    [Fact]
    public void A_short_browse_press_resolves_on_release()
    {
        var r = new GestureRecognizer();
        Assert.Null(r.Recognize(new ControllerEvent.BrowsePressed(), T0));
        Assert.Empty(r.Tick(T0.AddMilliseconds(500)));

        var g = r.Recognize(new ControllerEvent.BrowseReleased(), T0.AddMilliseconds(500));
        Assert.Equal(GestureIds.BrowsePressShort, g!.Id);
    }

    [Fact]
    public void A_long_browse_press_fires_while_still_held()
    {
        var r = new GestureRecognizer();
        r.Recognize(new ControllerEvent.BrowsePressed(), T0);

        Assert.Empty(r.Tick(T0.AddMilliseconds(999)));
        var due = r.Tick(T0.AddMilliseconds(1000));
        Assert.Single(due);
        Assert.Equal(GestureIds.BrowsePressHold, due[0].Id);
    }

    [Fact]
    public void The_hold_fires_once_only_and_the_release_adds_nothing()
    {
        var r = new GestureRecognizer();
        r.Recognize(new ControllerEvent.BrowsePressed(), T0);
        Assert.Single(r.Tick(T0.AddSeconds(1)));
        Assert.Empty(r.Tick(T0.AddSeconds(2)));
        Assert.Null(r.Recognize(new ControllerEvent.BrowseReleased(), T0.AddSeconds(3)));
    }

    [Fact]
    public void A_repeated_press_while_held_does_not_restart_the_clock()
    {
        // Some firmwares retransmit NoteOn while a button is held. The existing
        // Orchestrator guards against this ("leave a running timer be") and so must we,
        // or the hold would never become due.
        var r = new GestureRecognizer();
        r.Recognize(new ControllerEvent.BrowsePressed(), T0);
        r.Recognize(new ControllerEvent.BrowsePressed(), T0.AddMilliseconds(600));
        Assert.Single(r.Tick(T0.AddMilliseconds(1000)));
    }

    [Fact]
    public void The_side_ring_is_always_a_fine_nudge_even_with_shift_held()
    {
        var (_, go) = New();
        Assert.Equal(GestureIds.JogRingTurn,
            go(new ControllerEvent.JogRotated(0, 3, JogSource.SideRing))!.Id);

        go(new ControllerEvent.DeckShift(0, Pressed: true));
        Assert.Equal(GestureIds.JogRingTurn,
            go(new ControllerEvent.JogRotated(0, 3, JogSource.SideRing))!.Id);
    }

    [Fact]
    public void The_top_platter_becomes_a_search_while_that_decks_shift_is_held()
    {
        var (_, go) = New();
        Assert.Equal(GestureIds.JogTopTurn,
            go(new ControllerEvent.JogRotated(0, 3, JogSource.TopPlatter))!.Id);

        go(new ControllerEvent.DeckShift(0, Pressed: true));
        Assert.Equal(GestureIds.JogTopShiftTurn,
            go(new ControllerEvent.JogRotated(0, 3, JogSource.TopPlatter))!.Id);

        // The other deck's platter is unaffected by deck 0's Shift.
        Assert.Equal(GestureIds.JogTopTurn,
            go(new ControllerEvent.JogRotated(1, 3, JogSource.TopPlatter))!.Id);
    }

    [Fact]
    public void Platter_touch_is_tracked_so_the_faceplate_can_show_the_scratch_layer()
    {
        var (r, go) = New();
        Assert.Equal(GestureIds.JogTopTouch,
            go(new ControllerEvent.JogTouch(1, Touching: true))!.Id);
        Assert.True(r.IsPlatterTouched(1));
        Assert.False(r.IsPlatterTouched(0));

        go(new ControllerEvent.JogTouch(1, Touching: false));
        Assert.False(r.IsPlatterTouched(1));
    }

    [Fact]
    public void The_jog_delta_survives_on_the_source_event()
    {
        var (_, go) = New();
        var g = go(new ControllerEvent.JogRotated(0, -7, JogSource.TopPlatter))!;
        var src = Assert.IsType<ControllerEvent.JogRotated>(g.Source);
        Assert.Equal(-7, src.Delta);
    }

    [Fact]
    public void Every_declared_id_is_reachable_from_some_event()
    {
        // Guards against an id that exists in GestureIds but that nothing can emit —
        // the Faceplate would then carry a description no one ever sees.
        var r = new GestureRecognizer();
        var seen = new HashSet<string>();
        void Feed(ControllerEvent e)
        {
            var g = r.Recognize(e, T0);
            if (g is not null) seen.Add(g.Id);
        }

        Feed(new ControllerEvent.PlayPressed(0));
        Feed(new ControllerEvent.TransportCuePressed(0, false));
        Feed(new ControllerEvent.TransportCuePressed(0, true));
        Feed(new ControllerEvent.CueChanged(0, true));
        Feed(new ControllerEvent.MasterCueChanged(true));
        Feed(new ControllerEvent.BeatSyncPressed(0));
        Feed(new ControllerEvent.CyclePitchRange(0));
        Feed(new ControllerEvent.LoadToDeck(0));
        Feed(new ControllerEvent.JogRotated(0, 1, JogSource.TopPlatter));
        Feed(new ControllerEvent.JogRotated(0, 1, JogSource.SideRing));
        Feed(new ControllerEvent.JogTouch(0, true));
        Feed(new ControllerEvent.EqMoved(0, EqBand.High, 0.5));
        Feed(new ControllerEvent.FilterMoved(0, 0.5));
        Feed(new ControllerEvent.ChannelVolumeMoved(0, 0.5));
        Feed(new ControllerEvent.TempoMoved(0, 0.5));
        Feed(new ControllerEvent.CrossfaderMoved(0.5));
        Feed(new ControllerEvent.StemToggle(0, 0));
        Feed(new ControllerEvent.StemToggle(0, 1));
        Feed(new ControllerEvent.StemToggle(0, 2));
        Feed(new ControllerEvent.EchoToggle(0));
        Feed(new ControllerEvent.PadPageSelected(0, PadPage.HotCue));
        Feed(new ControllerEvent.PadPageSelected(0, PadPage.PadFx1));
        Feed(new ControllerEvent.BeatLoopToggle(0, 4));
        Feed(new ControllerEvent.BeatLoopHalve(0));
        Feed(new ControllerEvent.BeatLoopDouble(0));
        Feed(new ControllerEvent.NudgeGrid(-1, -1));
        Feed(new ControllerEvent.NudgeGrid(-1, +1));
        Feed(new ControllerEvent.BrowseRotated(1));
        Feed(new ControllerEvent.StemLevelMode(true));
        Feed(new ControllerEvent.EqMoved(0, EqBand.High, 0.5));   // now the stem variant
        Feed(new ControllerEvent.StemLevelMode(false));
        Feed(new ControllerEvent.DeckShift(0, true));
        Feed(new ControllerEvent.JogRotated(0, 1, JogSource.TopPlatter)); // now the shift variant
        Feed(new ControllerEvent.DeckShift(0, false));
        Feed(new ControllerEvent.BrowsePressed());
        Feed(new ControllerEvent.BrowseReleased());
        r.Recognize(new ControllerEvent.BrowsePressed(), T0);
        foreach (var g in r.Tick(T0.AddSeconds(2))) seen.Add(g.Id);

        Assert.Empty(GestureIds.All.Except(seen));
    }
}

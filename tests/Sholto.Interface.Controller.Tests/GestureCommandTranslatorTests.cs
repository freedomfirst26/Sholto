using Sholto.Data;
using Sholto.Interface.Controller.Gestures;

namespace Sholto.Interface.Controller.Tests;

public class GestureCommandTranslatorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly GestureRecognizer _recognizer = new();
    private readonly RecordingCommandSender _sender = new();
    private readonly GestureCommandTranslator _translator;

    public GestureCommandTranslatorTests() => _translator = new GestureCommandTranslator(_sender, _recognizer);

    private object? Run(ControllerEvent evt)
    {
        _sender.Sent.Clear();
        if (_recognizer.Recognize(evt, T0) is { } gesture) _translator.Translate(gesture);
        return _sender.Sent.SingleOrDefault();
    }

    [Fact]
    public void Every_catalog_gesture_sends_a_command_and_the_ones_with_no_app_effect_send_a_report()
    {
        var reportOnly = new HashSet<string>
        {
            GestureIds.CueTransportPlain, GestureIds.SyncPress,
            GestureIds.ShiftHold,
        };
        var events = new Dictionary<string, ControllerEvent>
        {
            [GestureIds.PlayPress] = new ControllerEvent.PlayPressed(0),
            [GestureIds.CueTransportPlain] = new ControllerEvent.TransportCuePressed(0, false),
            [GestureIds.CueTransportRestart] = new ControllerEvent.TransportCuePressed(0, true),
            [GestureIds.CueHeadphoneToggle] = new ControllerEvent.CueToggle(0),
            [GestureIds.MasterCueToggle] = new ControllerEvent.MasterCuePressed(),
            [GestureIds.SyncPress] = new ControllerEvent.BeatSyncPressed(0),
            [GestureIds.SyncCycleTempoRange] = new ControllerEvent.CycleTempoRange(0),
            [GestureIds.LoadPress] = new ControllerEvent.LoadToDeck(0),
            [GestureIds.JogTopTurn] = new ControllerEvent.JogRotated(0, 1, JogSource.TopPlatter),
            [GestureIds.JogRingTurn] = new ControllerEvent.JogRotated(0, 1, JogSource.SideRing),
            [GestureIds.JogTopTouch] = new ControllerEvent.JogTouch(0, true),
            [GestureIds.EqTurn] = new ControllerEvent.EqMoved(0, EqBand.Mid, 0.5),
            [GestureIds.FilterTurn] = new ControllerEvent.FilterMoved(0, 0.5),
            [GestureIds.VolumeMove] = new ControllerEvent.ChannelVolumeMoved(0, 0.5),
            [GestureIds.TempoMove] = new ControllerEvent.TempoMoved(0, 0.5),
            [GestureIds.CrossfaderMove] = new ControllerEvent.CrossfaderMoved(0.5),
            [GestureIds.PadStemDrums] = new ControllerEvent.StemToggle(0, 0),
            [GestureIds.PadEcho] = new ControllerEvent.EchoHold(0, true),
            [GestureIds.PadRoll] = new ControllerEvent.RollHold(0, true),
            [GestureIds.PadModeHotCue] = new ControllerEvent.PadPageSelected(0, PadPage.HotCue),
            [GestureIds.PadModePadFx1] = new ControllerEvent.PadPageSelected(0, PadPage.PadFx1),
            [GestureIds.PadModeBeatJump] = new ControllerEvent.PadPageSelected(0, PadPage.BeatJump),
            [GestureIds.PadModeSampler] = new ControllerEvent.PadPageSelected(0, PadPage.Sampler),
            [GestureIds.PadHotCuePress] = new ControllerEvent.HotCuePad(0, 3, false, true),
            [GestureIds.PadHotCueDelete] = new ControllerEvent.HotCuePad(0, 3, true, true),
            [GestureIds.PadBeatJump] = new ControllerEvent.BeatJumpPad(0, 5, 4, false),
            [GestureIds.PadBeatJumpHalve] = new ControllerEvent.BeatJumpPad(0, 6, -8, true),
            [GestureIds.PadBeatJumpDouble] = new ControllerEvent.BeatJumpPad(0, 7, 8, true),
            [GestureIds.PadStemSoloDrums] = new ControllerEvent.StemSolo(0, 0),
            [GestureIds.BeatLoopToggle] = new ControllerEvent.BeatLoopToggle(0, 4),
            [GestureIds.BeatLoopHalve] = new ControllerEvent.BeatLoopHalve(0),
            [GestureIds.BeatLoopDouble] = new ControllerEvent.BeatLoopDouble(0),
            [GestureIds.GridNudgeBack] = new ControllerEvent.NudgeGrid(-1, -1),
            [GestureIds.GridNudgeForward] = new ControllerEvent.NudgeGrid(-1, 1),
            [GestureIds.BrowseTurn] = new ControllerEvent.BrowseRotated(1),
            [GestureIds.ShiftHold] = new ControllerEvent.DeckShift(0, true),
        };

        foreach (var id in new GestureCatalog().All)
        {
            if (id is GestureIds.JogTopShiftTurn or GestureIds.EqStemLevelTurn or GestureIds.PadStemVocals
                or GestureIds.PadStemInstrumental or GestureIds.PadStemSoloVocals
                or GestureIds.PadStemSoloInstrumental or GestureIds.BrowsePressHold
                or GestureIds.BrowsePressShort) continue; // below, or in ControllerInputTests
            var sent = Run(events[id]);
            Assert.NotNull(sent);
            Assert.Equal(reportOnly.Contains(id), sent is ReportControl);
            Assert.Equal(InterfaceIds.Controller, ((IHasOrigin)sent!).Origin.InterfaceId);
            Assert.Equal(id, ((IHasOrigin)sent).Origin.GestureName);
        }
    }

    [Fact]
    public void A_top_platter_turn_carries_delta_surface_and_shift()
    {
        var plain = Assert.IsType<TurnPlatter>(Run(new ControllerEvent.JogRotated(1, -3, JogSource.TopPlatter)));
        Assert.Equal((1, -3, PlatterSurface.Top, false), (plain.Deck, plain.Delta, plain.Surface, plain.Shifted));

        Run(new ControllerEvent.DeckShift(1, true));
        var shifted = Assert.IsType<TurnPlatter>(Run(new ControllerEvent.JogRotated(1, 2, JogSource.TopPlatter)));
        Assert.Equal((PlatterSurface.Top, true), (shifted.Surface, shifted.Shifted));
        Assert.Equal("jog.top.shift.turn", shifted.Origin.GestureName);
    }

    [Fact]
    public void A_ring_turn_is_never_shifted_even_with_shift_held()
    {
        Run(new ControllerEvent.DeckShift(0, true));
        var ring = Assert.IsType<TurnPlatter>(Run(new ControllerEvent.JogRotated(0, 4, JogSource.SideRing)));
        Assert.Equal((PlatterSurface.SideRing, false), (ring.Surface, ring.Shifted));
    }

    [Fact]
    public void Touch_carries_the_decks_shift_state()
    {
        Assert.False(Assert.IsType<TouchPlatter>(Run(new ControllerEvent.JogTouch(0, true))).Shifted);
        Run(new ControllerEvent.DeckShift(0, true));
        Assert.True(Assert.IsType<TouchPlatter>(Run(new ControllerEvent.JogTouch(0, true))).Shifted);
        Assert.False(Assert.IsType<TouchPlatter>(Run(new ControllerEvent.JogTouch(1, true))).Shifted);
    }

    [Fact]
    public void The_deckless_beat_arrows_name_the_deck_whose_shift_is_held_or_none()
    {
        Assert.Equal(-1, Assert.IsType<NudgeGrid>(Run(new ControllerEvent.NudgeGrid(-1, 1))).Deck);

        Run(new ControllerEvent.DeckShift(1, true));
        var onDeck2 = Assert.IsType<NudgeGrid>(Run(new ControllerEvent.NudgeGrid(-1, -1)));
        Assert.Equal((1, -1), (onDeck2.Deck, onDeck2.Beats));

        Run(new ControllerEvent.DeckShift(0, true));
        Assert.Equal(0, Assert.IsType<NudgeGrid>(Run(new ControllerEvent.NudgeGrid(-1, 1))).Deck);
    }

    [Fact]
    public void A_per_deck_gesture_stamps_its_deck_side_on_the_origin()
    {
        var play = Assert.IsType<TogglePlay>(Run(new ControllerEvent.PlayPressed(1)));
        Assert.Equal(new Origin(InterfaceIds.Controller, "deck.play", "play.press", 1), play.Origin);
    }

    [Fact]
    public void The_beat_arrows_target_the_shift_deck_but_their_origin_names_no_deck()
    {
        // The arrows are one global pair: the command targets deck 1 (Shift held), the control has no deck side.
        Run(new ControllerEvent.DeckShift(1, true));
        var nudge = Assert.IsType<NudgeGrid>(Run(new ControllerEvent.NudgeGrid(-1, -1)));
        Assert.Equal(1, nudge.Deck);
        Assert.Equal(Origin.NoDeck, nudge.Origin.Deck);
    }

    [Fact]
    public void A_nudge_that_already_names_a_deck_keeps_it()
    {
        Run(new ControllerEvent.DeckShift(0, true));
        Assert.Equal(1, Assert.IsType<NudgeGrid>(Run(new ControllerEvent.NudgeGrid(1, 1))).Deck);
    }

    [Fact]
    public void Stem_level_turns_map_hi_mid_low_to_drums_vocals_instrumental()
    {
        Run(new ControllerEvent.DeckShift(0, true));
        Assert.Equal(0, Assert.IsType<SetStemLevel>(Run(new ControllerEvent.EqMoved(0, EqBand.High, 0.2))).Stem);
        Assert.Equal(1, Assert.IsType<SetStemLevel>(Run(new ControllerEvent.EqMoved(0, EqBand.Mid, 0.2))).Stem);
        Run(new ControllerEvent.DeckShift(1, true));
        var low = Assert.IsType<SetStemLevel>(Run(new ControllerEvent.EqMoved(1, EqBand.Low, 0.9)));
        Assert.Equal((1, 2, 1.0), (low.Deck, low.Stem, low.Value));
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.25, 0.5)]
    [InlineData(64 / 127.0, 1.0)]   // the controller's centre detent
    [InlineData(0.5, 1.0)]
    [InlineData(0.75, 1.0)]
    [InlineData(1.0, 1.0)]
    public void Stem_level_knob_centre_is_unity_left_fades_to_silence_and_right_stays_at_unity(double knob, double level)
    {
        Run(new ControllerEvent.DeckShift(0, true));
        var sent = Assert.IsType<SetStemLevel>(Run(new ControllerEvent.EqMoved(0, EqBand.Mid, knob)));
        Assert.Equal(level, sent.Value, 6);
    }

    [Fact]
    public void Shift_on_one_deck_leaves_the_other_decks_eq_plain()
    {
        Run(new ControllerEvent.DeckShift(0, true));
        var eq = Assert.IsType<SetEq>(Run(new ControllerEvent.EqMoved(1, EqBand.High, 0.7)));
        Assert.Equal((1, (int)EqBand.High, 0.7), (eq.Deck, eq.Band, eq.Value));
    }

    [Fact]
    public void Plain_eq_turns_carry_the_band_and_value_with_the_knobs_control_id()
    {
        var eq = Assert.IsType<SetEq>(Run(new ControllerEvent.EqMoved(1, EqBand.High, 0.7)));
        Assert.Equal((1, (int)EqBand.High, 0.7), (eq.Deck, eq.Band, eq.Value));
        Assert.Equal("mixer.eq.hi", eq.Origin.ControlId);
    }

    [Fact]
    public void Stem_pads_toggle_the_stem_in_the_events_group()
    {
        Assert.Equal(0, Assert.IsType<ToggleStem>(Run(new ControllerEvent.StemToggle(0, 0))).Stem);
        Assert.Equal(1, Assert.IsType<ToggleStem>(Run(new ControllerEvent.StemToggle(0, 1))).Stem);
        var instrumental = Assert.IsType<ToggleStem>(Run(new ControllerEvent.StemToggle(1, 2)));
        Assert.Equal((1, 2, "pad.sampler.instrumental"), (instrumental.Deck, instrumental.Stem, instrumental.Origin.GestureName));
    }

    [Fact]
    public void Echo_carries_both_edges()
    {
        var press = Assert.IsType<HoldEcho>(Run(new ControllerEvent.EchoHold(1, true)));
        Assert.Equal((1, true, "pad.padfx1.echo"), (press.Deck, press.Pressed, press.Origin.GestureName));
        Assert.Equal("deck.pad.1", press.Origin.ControlId);
        Assert.False(Assert.IsType<HoldEcho>(Run(new ControllerEvent.EchoHold(1, false))).Pressed);
    }

    [Fact]
    public void Roll_carries_both_edges()
    {
        Assert.True(Assert.IsType<HoldRoll>(Run(new ControllerEvent.RollHold(0, true))).Pressed);
        Assert.False(Assert.IsType<HoldRoll>(Run(new ControllerEvent.RollHold(0, false))).Pressed);
    }

    [Fact]
    public void Faders_and_the_browse_knob_carry_their_values()
    {
        Assert.Equal(0.25, Assert.IsType<SetCrossfader>(Run(new ControllerEvent.CrossfaderMoved(0.25))).Position);
        var vol = Assert.IsType<SetChannelVolume>(Run(new ControllerEvent.ChannelVolumeMoved(1, 0.8)));
        Assert.Equal((1, 0.8), (vol.Deck, vol.Value));
        Assert.Equal(-2, Assert.IsType<RotateBrowse>(Run(new ControllerEvent.BrowseRotated(-2))).Delta);
        Assert.Equal(4, Assert.IsType<ToggleBeatLoop>(Run(new ControllerEvent.BeatLoopToggle(0, 4))).Bars);
    }

    [Fact]
    public void Shift_plus_cue_restarts_and_plain_cue_only_reports()
    {
        var plain = Assert.IsType<ReportControl>(Run(new ControllerEvent.TransportCuePressed(0, false)));
        Assert.Equal(new Origin(InterfaceIds.Controller, "deck.cue.transport", "cue.transport.press", 0), plain.Origin);
        Assert.IsType<RestartTrack>(Run(new ControllerEvent.TransportCuePressed(0, true)));
    }

    [Fact]
    public void A_short_browse_press_reports_the_control_and_does_nothing_else()
    {
        Assert.Null(Run(new ControllerEvent.BrowsePressed()));
        var report = Assert.IsType<ReportControl>(Run(new ControllerEvent.BrowseReleased()));
        Assert.Equal(new Origin(InterfaceIds.Controller, "browse.knob", "browse.press.short"), report.Origin);
    }

    [Fact]
    public void The_shift_hold_reports_its_edge_and_names_the_release_distinctly()
    {
        var shiftOn = Assert.IsType<ReportControl>(Run(new ControllerEvent.DeckShift(1, true)));
        Assert.Equal((1, true, "deck.shift", "shift.hold"),
            (shiftOn.Origin.Deck, shiftOn.Pressed, shiftOn.Origin.ControlId, shiftOn.Origin.GestureName));
        var shiftOff = Assert.IsType<ReportControl>(Run(new ControllerEvent.DeckShift(1, false)));
        Assert.Equal((false, "shift.release"), (shiftOff.Pressed, shiftOff.Origin.GestureName));
    }

    [Fact]
    public void The_pad_mode_buttons_select_their_page()
    {
        Assert.Equal(PadPage.PadFx1, Assert.IsType<SelectPadPage>(Run(new ControllerEvent.PadPageSelected(1, PadPage.PadFx1))).Page);
        var hot = Assert.IsType<SelectPadPage>(Run(new ControllerEvent.PadPageSelected(0, PadPage.HotCue)));
        Assert.Equal((0, PadPage.HotCue), (hot.Deck, hot.Page));
        var jump = Assert.IsType<SelectPadPage>(Run(new ControllerEvent.PadPageSelected(1, PadPage.BeatJump)));
        Assert.Equal((1, PadPage.BeatJump, "deck.padmode.beatjump", "padmode.beatjump.press"),
            (jump.Deck, jump.Page, jump.Origin.ControlId, jump.Origin.GestureName));
        var sampler = Assert.IsType<SelectPadPage>(Run(new ControllerEvent.PadPageSelected(0, PadPage.Sampler)));
        Assert.Equal((PadPage.Sampler, "deck.padmode.sampler"), (sampler.Page, sampler.Origin.ControlId));
    }

    [Fact]
    public void A_hot_cue_pad_sends_both_edges_with_its_pad_control_id()
    {
        var down = Assert.IsType<PressHotCue>(Run(new ControllerEvent.HotCuePad(1, 3, false, true)));
        Assert.Equal((1, 3, true, "deck.pad.4", "pad.hotcue.press"),
            (down.Deck, down.Pad, down.Pressed, down.Origin.ControlId, down.Origin.GestureName));
        var up = Assert.IsType<PressHotCue>(Run(new ControllerEvent.HotCuePad(1, 3, false, false)));
        Assert.False(up.Pressed);
    }

    [Fact]
    public void A_shifted_hot_cue_pad_press_deletes_the_cue_and_its_release_sends_nothing()
    {
        var delete = Assert.IsType<DeleteHotCue>(Run(new ControllerEvent.HotCuePad(0, 6, true, true)));
        Assert.Equal((0, 6, "pad.hotcue.delete"), (delete.Deck, delete.Pad, delete.Origin.GestureName));
        Assert.Null(Run(new ControllerEvent.HotCuePad(0, 6, true, false)));
    }

    [Fact]
    public void A_plain_pad_note_with_shift_held_also_deletes_but_its_release_still_ends_a_preview()
    {
        Run(new ControllerEvent.DeckShift(0, true));

        Assert.IsType<DeleteHotCue>(Run(new ControllerEvent.HotCuePad(0, 2, false, true)));
        var release = Assert.IsType<PressHotCue>(Run(new ControllerEvent.HotCuePad(0, 2, false, false)));
        Assert.False(release.Pressed);
    }

    [Fact]
    public void A_beat_jump_pad_sends_the_beats_with_its_pad_control_id()
    {
        var jump = Assert.IsType<BeatJump>(Run(new ControllerEvent.BeatJumpPad(1, 5, -2, false)));
        Assert.Equal((1, -2.0, "deck.pad.6", "pad.beatjump.press"),
            (jump.Deck, jump.Beats, jump.Origin.ControlId, jump.Origin.GestureName));
    }

    [Fact]
    public void Shift_beat_jump_pad_seven_sends_halve_and_eight_sends_double_for_that_deck()
    {
        var halve = Assert.IsType<HalveBeatJump>(Run(new ControllerEvent.BeatJumpPad(1, 6, -8, true)));
        Assert.Equal((1, "deck.pad.7", "pad.beatjump.halve"), (halve.Deck, halve.Origin.ControlId, halve.Origin.GestureName));

        var grow = Assert.IsType<DoubleBeatJump>(Run(new ControllerEvent.BeatJumpPad(0, 7, 8, true)));
        Assert.Equal((0, "deck.pad.8", "pad.beatjump.double"), (grow.Deck, grow.Origin.ControlId, grow.Origin.GestureName));
    }

    [Fact]
    public void Shift_beat_jump_pads_one_to_six_send_nothing()
    {
        double[] beats = [-1, 1, -2, 2, -4, 4];
        for (var pad = 0; pad < 6; pad++)
            Assert.Null(Run(new ControllerEvent.BeatJumpPad(0, pad, beats[pad], true)));
    }

    [Fact]
    public void Held_shift_turns_plain_beat_jump_pads_seven_and_eight_into_size_changes()
    {
        Run(new ControllerEvent.DeckShift(1, true));

        Assert.IsType<HalveBeatJump>(Run(new ControllerEvent.BeatJumpPad(1, 6, -8, false)));
        Assert.IsType<DoubleBeatJump>(Run(new ControllerEvent.BeatJumpPad(1, 7, 8, false)));
        Assert.Null(Run(new ControllerEvent.BeatJumpPad(1, 0, -1, false)));
    }

    [Theory]
    [InlineData(0, "deck.pad.5", "pad.sampler.solo.drums")]
    [InlineData(1, "deck.pad.6", "pad.sampler.solo.vocals")]
    [InlineData(2, "deck.pad.7", "pad.sampler.solo.instrumental")]
    public void A_solo_pad_solos_the_stem_in_the_events_group(int group, string control, string gesture)
    {
        var solo = Assert.IsType<SoloStem>(Run(new ControllerEvent.StemSolo(1, group)));
        Assert.Equal((1, group, control, gesture), (solo.Deck, solo.Stem, solo.Origin.ControlId, solo.Origin.GestureName));
    }
}

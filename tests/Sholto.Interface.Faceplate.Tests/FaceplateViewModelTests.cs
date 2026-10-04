using System.Collections.Generic;
using Sholto.Data;
using Sholto.Interface.Faceplate.Devices.DdjFlx4;
using Sholto.Interface.Faceplate.Model;
using Sholto.Interface.Faceplate.ViewModels;
using Xunit;

namespace Sholto.Interface.Faceplate.Tests;

public class FaceplateViewModelTests
{
    // The guide data file's gesture names, as the controller stamps them on its commands' origins.
    private const string JogTopTurn = "jog.top.turn";
    private const string JogTopShiftTurn = "jog.top.shift.turn";
    private const string JogRingTurn = "jog.ring.turn";
    private const string CueTransportPlain = "cue.transport.press";
    private const string CueTransportRestart = "cue.transport.shift";
    private const string SyncCycleTempoRange = "sync.shift";
    private const string PlayPress = "play.press";
    private const string EqTurn = "eq.turn";
    private const string ShiftHold = "shift.hold";
    private const string ShiftRelease = "shift.release";

    private readonly RecordingCommandSender _sender = new();

    private FaceplateViewModel New() =>
        new(new FaceplateDocLoader().Load(new DdjFlx4Faceplate()), _sender);

    /// <summary>What the App publishes in Inspect mode when the controller sends a command.</summary>
    private static void Echo(FaceplateViewModel vm, string commandName, string controlId, string gesture, int deck = 0) =>
        vm.Handle(new CommandReceived(new Origin(InterfaceIds.Controller, controlId, gesture), commandName, deck));

    [Fact]
    public void The_panel_starts_closed_so_the_drawing_has_the_full_width()
    {
        Assert.False(New().IsPanelOpen);
    }

    [Fact]
    public void Selecting_a_control_opens_the_panel_and_lists_a_row_per_gesture()
    {
        var vm = New();
        vm.Select("deck.jog", deck: 0);

        Assert.True(vm.IsPanelOpen);
        Assert.Equal("deck.jog", vm.Selected!.Id);
        Assert.Contains(vm.Rows, r => r.GestureId == JogTopTurn);
        Assert.Contains(vm.Rows, r => r.GestureId == JogTopShiftTurn);
        Assert.Contains(vm.Rows, r => r.GestureId == JogRingTurn);
        Assert.True(vm.Rows.Count >= 4, "The jog has at least four ways to use it.");
    }

    [Fact]
    public void A_row_names_the_deck_it_applies_to()
    {
        var vm = New();
        vm.Select("deck.jog", deck: 1);
        Assert.Contains("2", vm.Title);   // "Jog wheel · deck 2"
    }

    [Fact]
    public void Hovering_a_row_highlights_the_partner_control()
    {
        var vm = New();
        vm.Select("deck.jog", deck: 0);
        vm.HoverRow(JogTopShiftTurn);
        Assert.Contains(vm.Highlighted, h => h.ControlId == "deck.shift");

        vm.HoverRow(null);
        Assert.Empty(vm.Highlighted);
    }

    [Fact]
    public void A_row_with_no_partner_highlights_nothing()
    {
        var vm = New();
        vm.Select("deck.jog", deck: 0);
        vm.HoverRow(JogRingTurn);
        Assert.Empty(vm.Highlighted);
    }

    [Fact]
    public void Hovering_a_chord_numbers_its_participants_in_order()
    {
        // Step 1 is the modifier you hold; step 2 is the control you then press.
        // Replaces the old "select a combination" flow — there is no separate
        // combination list any more, just a row like any other that happens to name a
        // partner.
        var vm = New();
        vm.Select("deck.shift", deck: 0);
        vm.HoverRow(CueTransportRestart);

        Assert.Contains(vm.Highlighted, h => h.ControlId == "deck.shift" && h.Step == 1);
        Assert.Contains(vm.Highlighted, h => h.ControlId == "deck.cue.transport" && h.Step == 2);
    }

    [Fact]
    public void Clicking_a_control_lists_every_gesture_it_is_involved_in_not_only_its_own()
    {
        // SHIFT owns only "hold" (its headline). The three shift chords all belong to
        // OTHER controls (the jog, transport CUE, BEAT SYNC) and only name SHIFT as a
        // partner — this is the involvement lookup the redesign exists for.
        var vm = New();
        vm.Select("deck.shift", deck: 0);

        Assert.Equal(3, vm.Rows.Count);
        Assert.Contains(vm.Rows, r => r.GestureId == JogTopShiftTurn && r.OwnerControlId == "deck.jog");
        Assert.Contains(vm.Rows, r => r.GestureId == CueTransportRestart && r.OwnerControlId == "deck.cue.transport");
        Assert.Contains(vm.Rows, r => r.GestureId == SyncCycleTempoRange && r.OwnerControlId == "deck.sync");
        // "hold" is the one, unambiguous plain gesture SHIFT owns, so it is the
        // headline rather than a fourth bullet.
        Assert.NotNull(vm.Headline);
        Assert.DoesNotContain(vm.Rows, r => r.OwnerControlId == "deck.shift");
    }

    [Fact]
    public void A_control_nothing_else_involves_lists_only_its_own_gestures()
    {
        var vm = New();
        vm.Select("deck.jog", deck: 0);

        // The jog has three plain gestures at once, so there is no single "plain
        // press" answer — no headline is extracted, and all four of its own gestures
        // stay in Rows. Nothing here should be owned by any other control.
        Assert.Null(vm.Headline);
        Assert.Equal(4, vm.Rows.Count);
        Assert.All(vm.Rows, r => Assert.Equal("deck.jog", r.OwnerControlId));
    }

    [Fact]
    public void A_control_with_one_plain_gesture_gets_it_as_the_headline()
    {
        var vm = New();
        vm.Select("deck.cue.transport", deck: 0);

        Assert.NotNull(vm.Headline);
        Assert.Equal(CueTransportPlain, vm.Headline!.GestureId);
        Assert.False(vm.Headline.Used, "A plain press of transport CUE does nothing.");
        // The shift chord is the control's own gesture too, but it names a partner so
        // it is never headline material — it stays a bullet.
        Assert.Single(vm.Rows);
        Assert.Equal(CueTransportRestart, vm.Rows[0].GestureId);
    }

    [Fact]
    public void A_control_with_no_gestures_and_no_involvement_still_opens_and_explains_itself()
    {
        var vm = New();
        vm.Select("mixer.trim", deck: 0);
        Assert.True(vm.IsPanelOpen);
        Assert.Null(vm.Headline);
        Assert.Empty(vm.Rows);
        Assert.False(string.IsNullOrWhiteSpace(vm.Selected!.Summary));
    }

    // ---- Task 12: live gestures --------------------------------------------------

    [Fact]
    public void A_received_command_selects_the_control_that_owns_it()
    {
        var vm = New();
        Echo(vm, "TurnPlatter", "deck.jog", JogTopShiftTurn);

        Assert.Equal("deck.jog", vm.Selected!.Id);
        Assert.True(vm.IsPanelOpen);
    }

    [Fact]
    public void A_received_command_highlights_the_row_not_just_the_control()
    {
        // The point of the probe: hold Shift, turn the platter, and the SEARCH row
        // (the shift-turn gesture) is what's active, not the plain scrub row — even
        // though both belong to the same control, deck.jog. Strengthened beyond a
        // single read: it starts on the plain turn's own row, and switching to the
        // shift chord moves ActiveRowId to a DIFFERENT row of the SAME still-selected
        // control — which is the actual claim ("marks the row, not just the control")
        // that a single assertion on one gesture cannot distinguish from ActiveRowId
        // simply mirroring whatever gesture id arrived most recently regardless of
        // control.
        var vm = New();
        Echo(vm, "TurnPlatter", "deck.jog", JogTopTurn);
        Assert.Equal("deck.jog", vm.Selected!.Id);
        Assert.Equal(JogTopTurn, vm.ActiveRowId);

        Echo(vm, "TurnPlatter", "deck.jog", JogTopShiftTurn);
        Assert.Equal("deck.jog", vm.Selected!.Id);   // same control throughout
        Assert.Equal(JogTopShiftTurn, vm.ActiveRowId);   // different row
    }

    [Fact]
    public void Clicking_a_control_clears_whatever_row_a_live_gesture_had_marked()
    {
        // A click names no gesture, so it must not leave a stale row marker from
        // before it — the panel it opens would show a row highlighted that nothing
        // actually just fired.
        var vm = New();
        Echo(vm, "TurnPlatter", "deck.jog", JogTopShiftTurn);
        Assert.NotNull(vm.ActiveRowId);

        vm.Select("deck.jog", deck: 0);
        Assert.Null(vm.ActiveRowId);
    }

    [Fact]
    public void Holding_a_modifier_switches_the_board_to_that_layer()
    {
        var vm = New();
        Echo(vm, "ReportControl", "deck.shift", ShiftHold);
        Assert.Equal("shift", vm.ActiveLayerId);

        Echo(vm, "ReportControl", "deck.shift", ShiftRelease);
        Assert.Equal("plain", vm.ActiveLayerId);
    }

    [Fact]
    public void Holding_a_modifier_does_not_select_anything()
    {
        // A DJ holding Shift to try a chord must not have the panel jump to the
        // SHIFT button itself.
        var vm = New();
        Echo(vm, "ReportControl", "deck.shift", ShiftHold);

        Assert.False(vm.IsPanelOpen);
        Assert.Null(vm.Selected);
    }

    [Fact]
    public void A_continuous_control_does_not_reselect_on_every_tick()
    {
        // Turning a knob fires many gestures a second. The panel must not thrash and
        // the shape must not strobe.
        var vm = New();
        Echo(vm, "SetEq", "mixer.eq.hi", EqTurn);
        var first = vm.Selected;
        int blinksAfterFirst = 0;
        vm.BlinkRequested += _ => blinksAfterFirst++;

        for (int i = 0; i < 50; i++) Echo(vm, "SetEq", "mixer.eq.hi", EqTurn);

        Assert.Same(first, vm.Selected);
        Assert.Equal(0, blinksAfterFirst);   // one blink on arrival, none for the other 50
    }

    [Fact]
    public void A_press_on_the_unit_blinks_the_shape_once()
    {
        var vm = New();
        var blinked = new List<string>();
        vm.BlinkRequested += blinked.Add;

        Echo(vm, "TogglePlay", "deck.play", PlayPress);
        Assert.Equal(["deck.play"], blinked);
    }

    [Fact]
    public void A_received_command_resolves_its_id_to_the_owning_control()
    {
        // The origin's control wins when it owns the gesture; otherwise the gesture name alone decides.
        // The resolved control is observable as the shape that blinks.
        var vm = New();
        var blinked = new List<string>();
        vm.BlinkRequested += blinked.Add;

        Echo(vm, "TogglePlay", "deck.play", PlayPress);
        Assert.Equal(["deck.play"], blinked);

        Echo(vm, "TurnPlatter", "deck.jog", JogTopShiftTurn);
        Assert.Equal(["deck.play", "deck.jog"], blinked);

        Echo(vm, "TogglePlay", "deck.nothing", "not.a.real.gesture", deck: 1);
        Assert.Equal(["deck.play", "deck.jog"], blinked);
    }
}

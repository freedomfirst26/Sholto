using Sholto.Data;
using Sholto.Interface.Faceplate.Devices.DdjFlx4;
using Sholto.Interface.Faceplate.Model;
using Sholto.Interface.Faceplate.ViewModels;
using Xunit;

namespace Sholto.Interface.Faceplate.Tests;

/// <summary>The guide follows the commands the App echoes in Inspect mode (CommandReceived) and the mode
/// itself (InspectModeChanged). Everything here goes through the Sholto.Data contract only: no controller or
/// keyboard types.</summary>
public class FaceplateCommandReceivedTests
{
    private readonly RecordingCommandSender _sender = new();
    private readonly List<string> _blinked = [];
    private readonly FaceplateViewModel _vm;

    public FaceplateCommandReceivedTests()
    {
        _vm = new FaceplateViewModel(new FaceplateDocLoader().Load(new DdjFlx4Faceplate()), _sender);
        _vm.BlinkRequested += _blinked.Add;
    }

    private void Receive(string interfaceId, string commandName, string controlId, string gesture, int deck = -1) =>
        _vm.Handle(new CommandReceived(new Origin(interfaceId, controlId, gesture, deck), commandName));

    private void FromController(string commandName, string controlId, string gesture, int deck = -1) =>
        Receive(InterfaceIds.Controller, commandName, controlId, gesture, deck);

    [Theory]
    [InlineData("key.p", "TogglePlay", "play.press", 0, "deck.play")]
    [InlineData("key.p", "TogglePlay", "play.press", 1, "deck.play")]
    [InlineData("key.1", "LoadSelectedIntoDeck", "load.press", 0, "deck.load")]
    [InlineData("key.2", "LoadSelectedIntoDeck", "load.press", 1, "deck.load")]
    public void The_keyboards_play_and_load_keys_explain_the_matching_control_on_the_unit(
        string key, string command, string gesture, int deck, string expectedControl)
    {
        Receive(InterfaceIds.Keyboard, command, key, gesture, deck);

        Assert.Equal(expectedControl, _vm.Selected!.Id);
        Assert.Equal(deck, _vm.SelectedDeck);
        Assert.Equal(gesture, _vm.ActiveRowId);
        Assert.Equal([expectedControl], _blinked);
    }

    [Theory]
    [InlineData("AddMarker", "key.m", "marker.add", 0)]
    [InlineData("OpenGridEditor", "key.g", "gridedit.open", -1)]
    public void The_keyboards_marker_and_grid_keys_have_no_control_on_the_unit_and_are_dropped(
        string command, string key, string gesture, int deck)
    {
        Receive(InterfaceIds.Keyboard, command, key, gesture, deck);

        Assert.Null(_vm.Selected);
        Assert.Null(_vm.ActiveRowId);
        Assert.Empty(_blinked);
    }

    [Fact]
    public void The_pad_roll_selects_pad_2_and_blinks()
    {
        FromController("HoldRoll", "deck.pad.2", "pad.padfx1.roll", deck: 0);

        Assert.Equal("deck.pad.2", _vm.Selected!.Id);
        Assert.Equal(0, _vm.SelectedDeck);
        Assert.Equal("pad.padfx1.roll", _vm.ActiveRowId);
        Assert.Equal(["deck.pad.2"], _blinked);
    }

    [Theory]
    [InlineData("mixer.eq.hi")]
    [InlineData("mixer.eq.mid")]
    [InlineData("mixer.eq.low")]
    public void The_three_eq_knobs_share_one_gesture_and_are_told_apart_by_the_origins_control(string control)
    {
        FromController("SetEq", control, "eq.turn", deck: 1);

        Assert.Equal(control, _vm.Selected!.Id);
        Assert.Equal(1, _vm.SelectedDeck);
    }

    [Theory]
    [InlineData("mixer.eq.hi")]
    [InlineData("mixer.eq.low")]
    public void A_stem_level_turn_selects_the_same_knob_on_its_own_row(string control)
    {
        FromController("SetStemLevel", control, "eq.stemlevel.turn", deck: 0);

        Assert.Equal(control, _vm.Selected!.Id);
        Assert.Equal("eq.stemlevel.turn", _vm.ActiveRowId);
    }

    [Fact]
    public void Shift_hold_switches_the_layer_without_selecting_anything()
    {
        FromController("ReportControl", "deck.shift", "shift.hold", deck: 0);
        Assert.Equal("shift", _vm.ActiveLayerId);
        FromController("ReportControl", "deck.shift", "shift.release", deck: 0);
        Assert.Equal("plain", _vm.ActiveLayerId);

        Assert.Null(_vm.Selected);
        Assert.Empty(_blinked);
    }

    [Fact]
    public void A_control_with_no_app_effect_is_still_explained()
    {
        // SYNC alone does nothing in the app, but the guide says so.
        FromController("ReportControl", "deck.sync", "sync.press", deck: 1);

        Assert.Equal("deck.sync", _vm.Selected!.Id);
        Assert.Equal(1, _vm.SelectedDeck);
    }

    [Fact]
    public void Holding_the_browse_knob_shows_the_reanalyse_row()
    {
        FromController("ReanalyzeSelected", "browse.knob", "browse.press.hold");

        Assert.Equal("browse.knob", _vm.Selected!.Id);
        Assert.Equal("browse.press.hold", _vm.ActiveRowId);
    }

    [Fact]
    public void A_short_browse_press_shows_the_does_nothing_row()
    {
        FromController("ReportControl", "browse.knob", "browse.press.short");

        Assert.Equal("browse.knob", _vm.Selected!.Id);
        Assert.Equal("browse.press.short", _vm.ActiveRowId);
        Assert.Contains("Does nothing", _vm.Selected.Gestures.Single(g => g.Id == "browse.press.short").Result);
        Assert.True(_vm.Selected.Gestures.Single(g => g.Id == "browse.press.short").Used);
    }

    [Fact]
    public void A_global_control_shows_no_deck_even_when_the_origin_names_one()
    {
        // The BEAT arrows are one pair shared by both decks; even if an origin names a deck, a global control shows none.
        FromController("NudgeGrid", "beat.nudge.back", "gridnudge.back", deck: 1);

        Assert.Equal("beat.nudge.back", _vm.Selected!.Id);
        Assert.Equal(-1, _vm.SelectedDeck);
    }

    [Fact]
    public void A_turning_knob_blinks_once_not_on_every_echo()
    {
        for (var i = 0; i < 50; i++) FromController("TurnPlatter", "deck.jog", "jog.top.turn", deck: 0);

        Assert.Equal(["deck.jog"], _blinked);
    }

    [Fact]
    public void Mounting_the_guide_sends_inspect_on_from_the_faceplate_and_unmounting_sends_it_off()
    {
        _vm.SetMounted(true);
        _vm.SetMounted(false);

        var on = Assert.IsType<SetInspectMode>(_sender.Sent[0]);
        var off = Assert.IsType<SetInspectMode>(_sender.Sent[1]);
        Assert.True(on.On);
        Assert.False(off.On);
        Assert.Equal(InterfaceIds.Faceplate, on.Origin.InterfaceId);
        Assert.Equal(InterfaceIds.Faceplate, off.Origin.InterfaceId);
    }

    [Fact]
    public void The_view_model_follows_the_apps_inspect_mode_and_drops_a_held_layer_when_it_ends()
    {
        _vm.Handle(new InspectModeChanged(true));
        Assert.True(_vm.IsInspecting);
        FromController("ReportControl", "deck.shift", "shift.hold", deck: 0);
        Assert.Equal("shift", _vm.ActiveLayerId);

        _vm.Handle(new InspectModeChanged(false));

        Assert.False(_vm.IsInspecting);
        Assert.Equal("plain", _vm.ActiveLayerId);
    }

    [Fact]
    public void A_view_model_subscribed_to_the_bus_follows_inspect_mode_and_echoed_commands()
    {
        var bus = new DataBus(new ThrowingFailureSink());
        var overlayDocLoader = new FaceplateDocLoader();
        // The overlay itself needs Avalonia; build the
        // view model and subscribe it the way FaceplateOverlayFactory does.
        var vm = new FaceplateViewModel(overlayDocLoader.Load(new DdjFlx4Faceplate()), bus);
        bus.Subscribe<CommandReceived>(vm);
        bus.Subscribe<InspectModeChanged>(vm);

        bus.Publish(new InspectModeChanged(true));
        bus.Publish(new CommandReceived(new Origin(InterfaceIds.Controller, "deck.play", "play.press", 1), "TogglePlay"));

        Assert.True(vm.IsInspecting);
        Assert.Equal("deck.play", vm.Selected!.Id);
    }
}

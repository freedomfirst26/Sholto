using Sholto.Controller;
using Sholto.Controller.Mappings;
using Xunit;

namespace Sholto.Controller.Tests;

public class ControllerModelTests
{
    [Fact]
    public void Button_Press_BubblesClickUp()
    {
        var b = new Button("cue");
        int clicks = 0;
        b.Clicked += _ => clicks++;
        b.Press();
        b.Press();
        Assert.Equal(2, clicks);
    }

    [Fact]
    public void ButtonWithLight_SetLit_AppliesOnChangeOnly()
    {
        var applied = new List<bool>();
        var b = new ButtonWithLight("cue", applied.Add);

        b.SetLit(true);   // change → apply
        b.SetLit(true);   // same  → no apply
        b.SetLit(false);  // change → apply

        Assert.False(b.IsLit);
        Assert.Equal([true, false], applied);
    }

    [Fact]
    public void ButtonWithLight_Reset_TurnsLightOff()
    {
        bool? last = null;
        var b = new ButtonWithLight("cue", on => last = on);
        b.SetLit(true);
        b.Reset();
        Assert.False(b.IsLit);
        Assert.False(last);   // hardware told to go dark
    }

    [Theory]
    [InlineData(0, LightFunction.Cue,       true,  new byte[] { 0x90, 0x54, 0x7F })]
    [InlineData(1, LightFunction.Cue,       false, new byte[] { 0x91, 0x54, 0x00 })]
    [InlineData(0, LightFunction.MasterCue, true,  new byte[] { 0x96, 0x63, 0x7F })]
    public void Flx4_RendersLight(int deck, LightFunction fn, bool on, byte[] expected)
    {
        var mapping = new DdjFlx4Mapping();
        Assert.Equal(expected, mapping.RenderLight(new ControllerLight(deck, fn), on));
    }

    [Fact]
    public void Flx4_UnknownDeckCue_RendersNothing()
    {
        var mapping = new DdjFlx4Mapping();
        Assert.Null(mapping.RenderLight(new ControllerLight(9, LightFunction.Cue), true));
    }

    [Fact]
    public void Fader_StartsUnmeasured()
    {
        var f = new Fader("vol");
        Assert.Null(f.Value);        // null = we haven't measured it
        Assert.False(f.Measured);
    }

    [Fact]
    public void Fader_FirstMove_AdoptsPosition_ThenTracks()
    {
        var f = new Fader("vol");
        var vals = new List<float>();
        f.ValueChanged += vals.Add;

        f.Move(0.5f);                // adopt — nothing to soft-takeover against
        Assert.True(f.Measured);
        Assert.Equal(0.5f, f.Value);

        f.Move(0.8f);                // tracks
        Assert.Equal(0.8f, f.Value);
        Assert.Equal([0.5f, 0.8f], vals);
    }

    [Fact]
    public void Fader_Reset_IsNoOp_PhysicalStateIsTruth()
    {
        var f = new Fader("vol");
        f.Move(0.7f);
        bool fired = false;
        f.ValueChanged += _ => fired = true;

        f.Reset();

        Assert.Equal(0.7f, f.Value);  // a slider isn't reset — its state is physical
        Assert.False(fired);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void Flx4_TransportCue_PressMapsToTransportCuePressed(int channel, int expectedDeck)
    {
        var mapping = new DdjFlx4Mapping();
        var evt = mapping.Translate(new NoteEvent(channel, 0x0C, 127, IsDown: true));
        var pressed = Assert.IsType<ControllerEvent.TransportCuePressed>(evt);
        Assert.Equal(expectedDeck, pressed.Deck);
        Assert.False(pressed.Shifted);
    }

    // Captured on hardware 2026-09-05: holding Shift and pressing CUE sends
    // ch=1/2 0x3F (Shift down), 0x48 down, 0x48 up, 0x3F up — the chord has its own note.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void Flx4_ShiftCue_ChordNoteMapsToShiftedTransportCue(int channel, int expectedDeck)
    {
        var mapping = new DdjFlx4Mapping();
        var evt = mapping.Translate(new NoteEvent(channel, 0x48, 127, IsDown: true));
        var pressed = Assert.IsType<ControllerEvent.TransportCuePressed>(evt);
        Assert.Equal(expectedDeck, pressed.Deck);
        Assert.True(pressed.Shifted);
        Assert.Null(mapping.Translate(new NoteEvent(channel, 0x48, 0, IsDown: false)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Flx4_TransportCue_ReleaseIsIgnored(int channel)
    {
        var mapping = new DdjFlx4Mapping();
        var evt = mapping.Translate(new NoteEvent(channel, 0x0C, 0, IsDown: false));
        Assert.Null(evt);
    }

    // Captured on hardware 2026-09-05: lifting a hand off the platter top sends
    // ch=1/2 NoteOff 0x36; touching sends the NoteOn.
    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(2, 1, true)]
    [InlineData(1, 0, false)]
    [InlineData(2, 1, false)]
    public void Flx4_JogTouch_BothEdgesMapToJogTouch(int channel, int expectedDeck, bool down)
    {
        var mapping = new DdjFlx4Mapping();
        var evt = mapping.Translate(new NoteEvent(channel, 0x36, down ? 127 : 0, IsDown: down));
        var touch = Assert.IsType<ControllerEvent.JogTouch>(evt);
        Assert.Equal(expectedDeck, touch.Deck);
        Assert.Equal(down, touch.Touching);
    }

    [Fact]
    public void Flx4_DeckShiftNote_StillMapsToDeckShift()
    {
        var mapping = new DdjFlx4Mapping();
        var evt = mapping.Translate(new NoteEvent(1, 0x3F, 127, IsDown: true));
        var shift = Assert.IsType<ControllerEvent.DeckShift>(evt);
        Assert.Equal(0, shift.Deck);
        Assert.True(shift.Pressed);
    }

    // Captured on hardware 2026-09-09 with SHOLTO_MIDI_LOG=1: the FX ON/OFF
    // (RELEASE FX) button sends ch=06 0x47. The option said channel 5, so the
    // modifier never fired and holding it just moved the EQ.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Flx4_StemLevelMode_ComesFromChannel6(bool down)
    {
        var mapping = new DdjFlx4Mapping();
        var evt = mapping.Translate(new NoteEvent(6, 0x47, down ? 127 : 0, IsDown: down));
        var mode = Assert.IsType<ControllerEvent.StemLevelMode>(evt);
        Assert.Equal(down, mode.Pressed);
    }

    [Fact]
    public void Flx4_Channel5_0x47_IsNotAControl()
    {
        // Nothing on the unit sends this. RELEASE FX, SMART CFX and SMART FADER were
        // all measured and none of them do.
        var mapping = new DdjFlx4Mapping();
        Assert.Null(mapping.Translate(new NoteEvent(5, 0x47, 127, IsDown: true)));
    }

    // ---- cue snapshot/restore (Faceplate LED repair, Task 12 fix round 1) --------
    //
    // The Faceplate guide's Inspect mode calls Controller.Reset() on the way back to
    // Play to repair LEDs the Controller changed while the app's own gesture table
    // was disabled (see App.axaml.cs). Reset() forces both decks' headphone cue AND
    // the master cue off with no memory of what they were — these tests are the
    // round trip that undoes that, so "cue deck 2, open the guide, close it" doesn't
    // silently kill a cue the DJ left running.

    [Fact]
    public void RestoreCueState_brings_back_a_cue_that_was_on_before_Reset()
    {
        var controller = new Controller();
        controller.Deck1Cue.Press();               // DJ turns deck 1 cue on
        Assert.True(controller.Deck1Cue.IsLit);
        var snapshot = controller.SnapshotCueState();

        controller.Reset();                         // guide closes: LED forced off
        Assert.False(controller.Deck1Cue.IsLit);

        var events = new List<ControllerEvent>();
        controller.Action += events.Add;
        controller.RestoreCueState(snapshot);

        Assert.True(controller.Deck1Cue.IsLit);
        Assert.Contains(events, e => e is ControllerEvent.CueChanged { Deck: 0, On: true });
    }

    [Fact]
    public void RestoreCueState_leaves_a_cue_that_was_off_alone_and_quiet()
    {
        // The restore must not simply turn everything back on: a cue that was
        // genuinely off before Reset must stay off, and nothing should be re-raised
        // for a button that already matches its snapshot.
        var controller = new Controller();
        var snapshot = controller.SnapshotCueState();   // nothing pressed — all off

        controller.Reset();

        var events = new List<ControllerEvent>();
        controller.Action += events.Add;
        controller.RestoreCueState(snapshot);

        Assert.False(controller.Deck1Cue.IsLit);
        Assert.False(controller.Deck2Cue.IsLit);
        Assert.False(controller.MasterCue.IsLit);
        Assert.DoesNotContain(events, e => e is ControllerEvent.CueChanged or ControllerEvent.MasterCueChanged);
    }

    [Fact]
    public void RestoreCueState_treats_each_button_independently()
    {
        // Deck 1 and master were on, deck 2 was off — the restore must reproduce
        // exactly that mix, not "all on" or "all off".
        var controller = new Controller();
        controller.Deck1Cue.Press();
        controller.MasterCue.Press();
        var snapshot = controller.SnapshotCueState();

        controller.Reset();
        controller.RestoreCueState(snapshot);

        Assert.True(controller.Deck1Cue.IsLit);
        Assert.False(controller.Deck2Cue.IsLit);
        Assert.True(controller.MasterCue.IsLit);
    }
}

using Sholto.Data;
using Sholto.Interface.Controller;
using Sholto.Interface.Controller.Mappings;
using Sholto.Interface.Controller.Mappings.DdjFlx4;
using Xunit;

namespace Sholto.Interface.Controller.Tests;

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
    // (RELEASE FX) button sends ch=06 0x47. Sholto gives it no job (stem levels
    // are SHIFT + EQ), so it must map to nothing on either edge.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Flx4_FxOnOff_IsNotMapped(bool down)
    {
        var mapping = new DdjFlx4Mapping();
        Assert.Null(mapping.Translate(new NoteEvent(6, 0x47, down ? 127 : 0, IsDown: down)));
    }

    [Fact]
    public void Flx4_Channel5_0x47_IsNotAControl()
    {
        // Nothing on the unit sends this. RELEASE FX, SMART CFX and SMART FADER were
        // all measured and none of them do.
        var mapping = new DdjFlx4Mapping();
        Assert.Null(mapping.Translate(new NoteEvent(5, 0x47, 127, IsDown: true)));
    }

    // ---- pad-mode forcing: the App's page wins over the hardware's own ------------

    [Theory]
    [InlineData(0, PadPage.HotCue, new byte[] { 0x90, 0x1B, 0x7F, 0x90, 0x1B, 0x00 })]
    [InlineData(1, PadPage.HotCue, new byte[] { 0x91, 0x1B, 0x7F, 0x91, 0x1B, 0x00 })]
    [InlineData(0, PadPage.PadFx1, new byte[] { 0x90, 0x1E, 0x7F, 0x90, 0x1E, 0x00 })]
    [InlineData(1, PadPage.PadFx1, new byte[] { 0x91, 0x1E, 0x7F, 0x91, 0x1E, 0x00 })]
    public void Flx4_RendersPadModePressSimulation(int deck, PadPage page, byte[] expected)
    {
        Assert.Equal(expected, new DdjFlx4Mapping().RenderPadMode(deck, page));
    }

    [Fact]
    public void Flx4_PadModeForPagesWithoutAButton_IsNull()
    {
        var mapping = new DdjFlx4Mapping();
        Assert.Null(mapping.RenderPadMode(0, PadPage.BeatJump));
        Assert.Null(mapping.RenderPadMode(0, PadPage.Sampler));
        Assert.Null(mapping.RenderPadMode(2, PadPage.HotCue));
    }

    [Fact]
    public void Flx4_StartupInit_IsTheHotCuePressSimulationForBothDecks()
    {
        var mapping = new DdjFlx4Mapping();
        Assert.Equal(
            mapping.RenderPadMode(0, PadPage.HotCue)!.Concat(mapping.RenderPadMode(1, PadPage.HotCue)!),
            mapping.StartupInit());
    }

    [Fact]
    public void SetPadPage_forces_the_hardware_into_the_apps_page_with_the_press_bytes()
    {
        var midi = new FakeMidiConnection();
        var controller = new Controller(midi);

        controller.SetPadPage(1, PadPage.PadFx1);

        Assert.Contains(new byte[] { 0x91, 0x1E, 0x7F, 0x91, 0x1E, 0x00 }, midi.Sent, new BytesComparer());
    }

    [Fact]
    public void SetPadPage_lights_the_matching_mode_button_and_darkens_the_other()
    {
        var controller = new Controller(new FakeMidiConnection());

        controller.SetPadPage(0, PadPage.PadFx1);
        Assert.True(controller.Deck1PadModePadFx1.IsLit);
        Assert.False(controller.Deck1PadModeHotCue.IsLit);

        controller.SetPadPage(0, PadPage.HotCue);
        Assert.True(controller.Deck1PadModeHotCue.IsLit);
        Assert.False(controller.Deck1PadModePadFx1.IsLit);
        Assert.False(controller.Deck2PadModeHotCue.IsLit);
    }

    [Fact]
    public void SetPadPage_does_not_press_again_when_the_hardware_is_already_in_that_page()
    {
        // A repeated PAD FX1 press can step the device on to another page, so a page the
        // hardware itself just entered (a wire press) must not be forced a second time.
        var midi = new FakeMidiConnection();
        var controller = new Controller(midi);
        controller.Connect();
        midi.Raise(new ControllerEvent.PadPageSelected(0, PadPage.PadFx1));
        midi.Sent.Clear();

        controller.SetPadPage(0, PadPage.PadFx1);

        Assert.DoesNotContain(midi.Sent, b => b.Length == 6);
    }

    [Fact]
    public void SetPadPage_forces_the_hardware_back_after_a_press_the_app_ignored()
    {
        // Inspect: the DJ pressed PAD FX1, the App's table was off, its page stayed HotCue.
        var midi = new FakeMidiConnection();
        var controller = new Controller(midi);
        controller.Connect();
        midi.Raise(new ControllerEvent.PadPageSelected(0, PadPage.PadFx1));
        midi.Sent.Clear();

        controller.SetPadPage(0, PadPage.HotCue);

        Assert.Contains(new byte[] { 0x90, 0x1B, 0x7F, 0x90, 0x1B, 0x00 }, midi.Sent, new BytesComparer());
    }

    [Fact]
    public void After_a_reconnect_the_apps_page_is_forced_again()
    {
        var midi = new FakeMidiConnection();
        var controller = new Controller(midi);
        controller.Connect();
        controller.SetPadPage(0, PadPage.PadFx1);
        midi.Sent.Clear();
        controller.SetPadPage(0, PadPage.PadFx1);
        Assert.DoesNotContain(midi.Sent, b => b.Length == 6);   // already there

        midi.RaiseConnected();                                   // replug: comes up in Hot Cue
        midi.Sent.Clear();
        controller.SetPadPage(0, PadPage.PadFx1);                // adapter replays the App's page

        Assert.Contains(new byte[] { 0x90, 0x1E, 0x7F, 0x90, 0x1E, 0x00 }, midi.Sent, new BytesComparer());
    }

    [Fact]
    public void A_cue_press_is_forwarded_without_the_controller_lighting_anything()
    {
        var midi = new FakeMidiConnection();
        var controller = new Controller(midi);
        controller.Connect();
        var events = new List<ControllerEvent>();
        controller.Action += events.Add;

        midi.Raise(new ControllerEvent.CueToggle(0));
        midi.Raise(new ControllerEvent.MasterCuePressed());

        Assert.Equal(2, events.Count);
        Assert.IsType<ControllerEvent.CueToggle>(events[0]);
        Assert.IsType<ControllerEvent.MasterCuePressed>(events[1]);
        Assert.False(controller.Deck1Cue.IsLit);
        Assert.False(controller.MasterCue.IsLit);
    }

    [Fact]
    public void Cue_lights_follow_the_set_calls_only()
    {
        var controller = new Controller(new FakeMidiConnection());
        controller.SetHeadphoneCueLight(1, true);
        controller.SetMasterCueLight(true);
        Assert.True(controller.Deck2Cue.IsLit);
        Assert.False(controller.Deck1Cue.IsLit);
        Assert.True(controller.MasterCue.IsLit);
    }
}

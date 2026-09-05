using Sholto.Controller;
using Sholto.Controller.Mappings;
using Xunit;

namespace Sholto.Controller.Tests;

public class ControllerModelTests
{
    [Fact]
    public void Button_Press_BubblesClickUp()
    {
        var b = new Button("cue", _ => { });
        int clicks = 0;
        b.Clicked += _ => clicks++;
        b.Press();
        b.Press();
        Assert.Equal(2, clicks);
    }

    [Fact]
    public void Button_SetLit_AppliesOnChangeOnly()
    {
        var applied = new List<bool>();
        var b = new Button("cue", applied.Add);

        b.SetLit(true);   // change → apply
        b.SetLit(true);   // same  → no apply
        b.SetLit(false);  // change → apply

        Assert.True(b.IsLit is false);
        Assert.Equal([true, false], applied);
    }

    [Fact]
    public void Button_Reset_TurnsLightOff()
    {
        bool? last = null;
        var b = new Button("cue", on => last = on);
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

    [Fact]
    public void Flx4_DeckShiftNote_StillMapsToDeckShift()
    {
        var mapping = new DdjFlx4Mapping();
        var evt = mapping.Translate(new NoteEvent(1, 0x3F, 127, IsDown: true));
        var shift = Assert.IsType<ControllerEvent.DeckShift>(evt);
        Assert.Equal(0, shift.Deck);
        Assert.True(shift.Pressed);
    }
}

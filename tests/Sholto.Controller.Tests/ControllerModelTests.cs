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
    public void Fader_SoftTakeover_IgnoresMovesUntilItCrossesTheValue()
    {
        var f = new Fader("vol");          // starts value 0, disengaged
        var vals = new List<float>();
        f.ValueChanged += vals.Add;

        f.Move(0.5f);                       // first sample; no crossing yet
        f.Move(0.8f);                       // moving away from 0 — still no pickup
        Assert.False(f.Engaged);
        Assert.Empty(vals);                 // nothing sent while catching up

        f.Move(0.0f);                       // crosses the software value (0) → engage
        Assert.True(f.Engaged);

        f.Move(0.3f);                       // now it tracks
        Assert.Equal(0.3f, f.Value);
        Assert.Equal([0.3f], vals);
    }

    [Fact]
    public void Fader_Reset_IsNoOp_PhysicalStateIsTruth()
    {
        var f = new Fader("vol");
        f.Move(0.5f); f.Move(0.0f); f.Move(0.7f);   // pick up and move to 0.7
        Assert.True(f.Engaged);

        bool fired = false;
        f.ValueChanged += _ => fired = true;
        f.Reset();

        Assert.True(f.Engaged);              // untouched — a slider isn't reset
        Assert.Equal(0.7f, f.Value);
        Assert.False(fired);
    }
}

using Sholto.Controller;
using Sholto.Controller.Mappings;
using Xunit;

namespace Sholto.Controller.Tests;

public class FeedbackTests
{
    [Fact]
    public void Bus_DeDupes_OnlyBroadcastsRealChanges()
    {
        var bus = new FeedbackBus();
        int fired = 0;
        bus.LightChanged += _ => fired++;

        bus.SetLight(0, LightFunction.Cue, true);   // change → fire
        bus.SetLight(0, LightFunction.Cue, true);   // same → no fire
        bus.SetLight(0, LightFunction.Cue, false);  // change → fire

        Assert.Equal(2, fired);
    }

    [Fact]
    public void Bus_TracksLightsIndependently()
    {
        var bus = new FeedbackBus();
        var events = new List<LightChanged>();
        bus.LightChanged += events.Add;

        bus.SetLight(0, LightFunction.Cue, true);
        bus.SetLight(1, LightFunction.Cue, true);   // different deck → separate light
        bus.SetLight(0, LightFunction.Play, true);  // different function → separate light

        Assert.Equal(3, events.Count);
    }

    [Theory]
    [InlineData(0, LightFunction.Cue,  true,  new byte[] { 0x90, 0x54, 0x7F })]
    [InlineData(1, LightFunction.Cue,  false, new byte[] { 0x91, 0x54, 0x00 })]
    [InlineData(0, LightFunction.Play, true,  new byte[] { 0x90, 0x0B, 0x7F })]
    [InlineData(1, LightFunction.Loop, true,  new byte[] { 0x91, 0x4D, 0x7F })]
    public void Flx4_RendersLight_ToExpectedBytes(int deck, LightFunction fn, bool on, byte[] expected)
    {
        var mapping = new DdjFlx4Mapping();
        var bytes = mapping.RenderLight(new ControllerLight(deck, fn), on);
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Flx4_UnknownDeck_RendersNothing()
    {
        var mapping = new DdjFlx4Mapping();
        Assert.Null(mapping.RenderLight(new ControllerLight(5, LightFunction.Cue), true));
    }
}

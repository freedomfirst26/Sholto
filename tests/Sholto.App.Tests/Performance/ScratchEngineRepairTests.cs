using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>Regression coverage for the Inspect-mode "stranded platter grab" bug: a hand resting on a top
/// platter when the guide opens never gets its lift event (the Inspect gate echoes the controller's commands
/// instead of running them), so ScratchState.Touching would otherwise stay true forever and silence the deck.
/// Drives the fix through the Inspect mode event alone — no controller hardware needed — seeding the engine's
/// public per-deck state the way a real platter touch would, since a grab needs a scratch-capable Deck (real
/// audio + a varispeed provider).</summary>
public class ScratchEngineRepairTests
{
    [Fact]
    public void InspectOnThenOff_WithActiveGrabStranded_ClearsTouching()
    {
        var rig = new PerformanceRig();
        var deck0State = rig.Scratch.StateOf(0);

        // Simulate: a hand grabbed the platter (Active + Touching both true) just
        // before the guide opened, and its later lift never arrived because Inspect
        // mode silenced the input interfaces.
        deck0State.Active = true;
        deck0State.Touching = true;

        // Inspect on, then off, as the guide's mount/unmount drive it. Turning it on changes nothing.
        rig.Scratch.Handle(new InspectModeChanged(true));
        Assert.True(deck0State.Touching);
        rig.Scratch.Handle(new InspectModeChanged(false));

        Assert.False(deck0State.Touching, "a stranded grab must release its Touching flag on return to Play");
        // Active is left alone — the engine's own decay path (not this repair) is
        // what takes the deck the rest of the way back to rest.
        Assert.True(deck0State.Active);
    }

    [Fact]
    public void ReleaseStrandedTouches_LeavesInactiveDeckAlone()
    {
        var rig = new PerformanceRig();
        var deck1State = rig.Scratch.StateOf(1);

        // No grab in flight: nothing should be touched.
        Assert.False(deck1State.Active);
        Assert.False(deck1State.Touching);

        rig.Scratch.ReleaseStrandedTouches();

        Assert.False(deck1State.Touching);
    }
}

using Sholto.Data;
using Sholto.App.Performance;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>Regression coverage for the IDeckHost split: jog recency used to live as settable properties on
/// MainViewModel, written and read back by the coordinator. It now lives in <see cref="JogRecency"/>, and the
/// magnetism state machine (previously MainViewModel.MagnetismFactor/UpdateMagnetism, reading that same jog
/// recency) is <see cref="MagnetSnap"/>. This drives a real jog command end to end — the platter command
/// handler, the jog accumulator, the frame's flush and magnetism pass — through the buckets' public surfaces,
/// to prove the plumbing survived the move, not just that it compiles.</summary>
public class PlatterJogRoutingTests
{
    [Fact]
    public void SideRingJog_UpdatesJogRecency_ForTheJoggedDeck()
    {
        var rig = new PerformanceRig();
        rig.Tick.Start();

        // deck 0 (top-left "Deck 1" in gesture terms), side ring so it takes the
        // silent-seek accumulator path (no loaded/scratch-capable deck needed).
        var command = new TurnPlatter(0, Delta: 5, PlatterSurface.SideRing, Shifted: false,
            new Origin(InterfaceIds.Controller, "deck.jog", "jog.ring.turn"));
        rig.Platter.Handle(in command);

        Assert.Equal(1, rig.Recency.LastJoggedDeck);
        Assert.NotEqual(DateTime.MinValue, rig.Recency.LastJogAt);
        Assert.NotEqual(DateTime.MinValue, rig.Recency.LastJogAt1);
        Assert.Equal(DateTime.MinValue, rig.Recency.LastJogAt2);

        // The frame must flush the pending jog and run the magnetism pass without throwing, even with
        // nothing loaded on either deck (HasAnalysis false short-circuits eligibility quickly).
        Assert.NotEqual(0.0, rig.Seek.PendingSeconds(0));

        var ex = Record.Exception(rig.Clock.Tick);
        Assert.Null(ex);

        // The frame flushed the accumulator back to zero.
        Assert.Equal(0.0, rig.Seek.PendingSeconds(0));
    }

    [Fact]
    public void ScratchEnd_ClearsJogRecency_ForThatDeckOnly()
    {
        var rig = new PerformanceRig();

        rig.Recency.MarkJogged(0); // deck 0 → LastJoggedDeck=1, LastJogAt/LastJogAt1 set
        rig.Recency.MarkJogged(1); // deck 1 → LastJoggedDeck=2, LastJogAt/LastJogAt2 set

        rig.Recency.ClearAfterScratchEnd(true); // "deck 1 (isDeck1) just finished a scratch"

        Assert.Equal(DateTime.MinValue, rig.Recency.LastJogAt);
        Assert.Equal(DateTime.MinValue, rig.Recency.LastJogAt1);
        // Deck 2's own stamp is untouched — only deck 1's scratch ended.
        Assert.NotEqual(DateTime.MinValue, rig.Recency.LastJogAt2);
    }
}

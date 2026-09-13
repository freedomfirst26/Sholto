using System;
using System.Reflection;
using Microsoft.Extensions.Options;
using Sholto.App.ViewModels;
using Sholto.Controller;
using Sholto.Controller.Gestures;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>Regression coverage for the IDeckHost split (see the split plan in
/// ~/Projects/sholto.md, "give the jog state a home"): jog recency used to live as
/// settable properties on MainViewModel, written and read back by Orchestrator.
/// It now lives entirely in Orchestrator's own private JogTracker, and the
/// magnetism state machine (previously MainViewModel.MagnetismFactor/
/// UpdateMagnetism, reading that same jog recency) moved alongside it. This drives
/// a real jog gesture end to end — HandleGesture → the jog accumulator → Tick's
/// flush + magnetism pass — and inspects the private JogTracker via reflection
/// (same technique OrchestratorScratchRepairTests already uses for ScratchState)
/// to prove the plumbing survived the move, not just that it compiles.</summary>
public class OrchestratorJogRoutingTests
{
    private static Orchestrator MakeOrchestrator(out MainViewModel vm)
    {
        AvaloniaTestApp.EnsureStarted();
        var decoder = new Sholto.Audio.AudioFileDecoder([]);
        var session = new NullExternalTool();
        var stemAnalyzer = new Sholto.Analysis.DemucsStemAnalyzer(session);
        vm = new MainViewModel(
            Options.Create(new FeatureOptions()),
            decoder, stemAnalyzer,
            new Sholto.App.Theming.ThemeContext(), Sholto.Audio.NullLoopDebug.Instance, stemAnalyzer,
            new Sholto.Library.TrackScanner());
        return new Orchestrator(vm, vm, vm, vm, () => null,
            Options.Create(new ScratchOptions()), Options.Create(new MagnetismOptions()),
            new GestureRecognizer(), decoder);
    }

    private static object GetJogTracker(Orchestrator orchestrator)
    {
        var field = typeof(Orchestrator).GetField("_jog", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Orchestrator._jog not found — has JogTracker been renamed?");
        return field.GetValue(orchestrator)!;
    }

    [Fact]
    public void SideRingJog_UpdatesJogTracker_ForTheJoggedDeck()
    {
        var orchestrator = MakeOrchestrator(out _);
        var jog = GetJogTracker(orchestrator);
        var jogType = jog.GetType();

        // deck 0 (top-left "Deck 1" in gesture terms), side ring so it takes the
        // silent-seek accumulator path (no loaded/scratch-capable deck needed).
        var gesture = new Gesture(GestureIds.JogRingTurn, 0,
            new ControllerEvent.JogRotated(0, Delta: 5, Source: JogSource.SideRing));
        orchestrator.HandleGesture(gesture);

        Assert.Equal(1, (int)jogType.GetProperty("LastJoggedDeck")!.GetValue(jog)!);
        Assert.NotEqual(DateTime.MinValue, (DateTime)jogType.GetProperty("LastJogAt")!.GetValue(jog)!);
        Assert.NotEqual(DateTime.MinValue, (DateTime)jogType.GetProperty("LastJogAt1")!.GetValue(jog)!);
        Assert.Equal(DateTime.MinValue, (DateTime)jogType.GetProperty("LastJogAt2")!.GetValue(jog)!);

        // Tick() must flush the pending jog and run the (now Orchestrator-owned)
        // magnetism pass without throwing, even with nothing loaded on either deck
        // (HasAnalysis false short-circuits IsBpmEligibleForMagnetism quickly —
        // this is exactly the path that used to run inside MainViewModel).
        var pending1Field = typeof(Orchestrator).GetField("_pendingJog1", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.NotEqual(0.0, (double)pending1Field.GetValue(orchestrator)!);

        var ex = Record.Exception(orchestrator.Tick);
        Assert.Null(ex);

        // Tick() flushed the accumulator back to zero.
        Assert.Equal(0.0, (double)pending1Field.GetValue(orchestrator)!);
    }

    [Fact]
    public void ScratchEnd_ClearsJogRecency_ForThatDeckOnly()
    {
        var orchestrator = MakeOrchestrator(out _);
        var jog = GetJogTracker(orchestrator);
        var jogType = jog.GetType();
        var clear = jogType.GetMethod("ClearAfterScratchEnd", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("JogTracker.ClearAfterScratchEnd not found — has it been renamed?");

        var mark = jogType.GetMethod("MarkJogged", BindingFlags.Public | BindingFlags.Instance)!;
        mark.Invoke(jog, [0]); // deck 0 → LastJoggedDeck=1, LastJogAt/LastJogAt1 set
        mark.Invoke(jog, [1]); // deck 1 → LastJoggedDeck=2, LastJogAt/LastJogAt2 set

        clear.Invoke(jog, [true]); // "deck 1 (isDeck1) just finished a scratch"

        Assert.Equal(DateTime.MinValue, (DateTime)jogType.GetProperty("LastJogAt")!.GetValue(jog)!);
        Assert.Equal(DateTime.MinValue, (DateTime)jogType.GetProperty("LastJogAt1")!.GetValue(jog)!);
        // Deck 2's own stamp is untouched — only deck 1's scratch ended.
        Assert.NotEqual(DateTime.MinValue, (DateTime)jogType.GetProperty("LastJogAt2")!.GetValue(jog)!);
    }
}

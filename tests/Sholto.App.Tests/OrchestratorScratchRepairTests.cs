using System;
using System.Reflection;
using Microsoft.Extensions.Options;
using Sholto.App.ViewModels;
using Sholto.Controller.Gestures;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>Regression coverage for the Inspect-mode "stranded platter grab" bug: a hand
/// resting on a top platter when the guide opens never gets its lift event (the App's
/// gesture table is disabled for Inspect), so ScratchState.Touching would otherwise stay
/// true forever and silence the deck. Drives the fix through routing alone — no
/// controller hardware needed — using reflection only to seed the private scratch
/// state the way a real JogTouch(true) would, since Orchestrator has no other way to
/// simulate a grab without a scratch-capable Deck (real audio + a varispeed provider).</summary>
public class OrchestratorScratchRepairTests
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
            new Sholto.Music.TrackScanner());
        return new Orchestrator(vm, vm, vm, vm, () => null,
            Options.Create(new ScratchOptions()), Options.Create(new MagnetismOptions()),
            new GestureRecognizer(), decoder);
    }

    private static object GetScratchState(Orchestrator orchestrator, int deck)
    {
        var field = typeof(Orchestrator).GetField("_scratch", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Orchestrator._scratch not found — has it been renamed?");
        var array = (Array)field.GetValue(orchestrator)!;
        return array.GetValue(deck)!;
    }

    [Fact]
    public void PlayToInspectToPlay_WithActiveGrabStranded_ClearsTouching()
    {
        var orchestrator = MakeOrchestrator(out var vm);
        var deck0State = GetScratchState(orchestrator, 0);
        var stateType = deck0State.GetType();
        var activeField = stateType.GetField("Active")!;
        var touchingField = stateType.GetField("Touching")!;

        // Simulate: a hand grabbed the platter (Active + Touching both true) just
        // before the guide opened, and its later lift never arrived because Inspect
        // mode disabled the app's gesture table.
        activeField.SetValue(deck0State, true);
        touchingField.SetValue(deck0State, true);

        // Play -> Inspect -> Play, exactly as the top-bar button / Esc drive it.
        vm.IsFaceplateOpen = true;
        Assert.Equal(GestureRouting.Inspect, vm.GestureRouting);
        vm.IsFaceplateOpen = false;
        Assert.Equal(GestureRouting.Play, vm.GestureRouting);

        // The routing handler in App.axaml.cs is what actually calls this on the way
        // back to Play; call it directly here since App.axaml.cs's composition root
        // isn't under test.
        orchestrator.ReleaseStrandedScratchTouches();

        Assert.False((bool)touchingField.GetValue(deck0State)!, "a stranded grab must release its Touching flag on return to Play");
        // Active is left alone — TickScratch's own decay path (not this repair) is
        // what takes the deck the rest of the way back to rest.
        Assert.True((bool)activeField.GetValue(deck0State)!);
    }

    [Fact]
    public void ReleaseStrandedScratchTouches_LeavesInactiveDeckAlone()
    {
        var orchestrator = MakeOrchestrator(out _);
        var deck1State = GetScratchState(orchestrator, 1);
        var stateType = deck1State.GetType();
        var activeField = stateType.GetField("Active")!;
        var touchingField = stateType.GetField("Touching")!;

        // No grab in flight: nothing should be touched.
        Assert.False((bool)activeField.GetValue(deck1State)!);
        Assert.False((bool)touchingField.GetValue(deck1State)!);

        orchestrator.ReleaseStrandedScratchTouches();

        Assert.False((bool)touchingField.GetValue(deck1State)!);
    }
}

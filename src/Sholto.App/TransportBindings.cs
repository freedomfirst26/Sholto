using Sholto.Controller;
using Sholto.Controller.Gestures;

namespace Sholto.App;

/// <summary>Gesture bindings for transport: play, both CUE buttons (headphone +
/// master), and sync. Extracted verbatim from <c>Orchestrator.HandleGesture</c> as
/// part of dissolving its 34-arm switch into the gesture binding table — see
/// <c>~/Projects/sholto.md</c>. Depends only on <see cref="IApplication"/> plus a
/// callback for the one event-shaped case (<c>MasterCueToggle</c>), so it is
/// testable with a fake deck and no real Orchestrator.</summary>
public static class TransportBindings
{
    /// <param name="app">The app (deck/mixer access) — same object Orchestrator holds.</param>
    /// <param name="playPressed">Play/pause a deck — Orchestrator's own
    /// <c>PlayPressed</c>, shared with the keyboard's Play action.</param>
    /// <param name="masterCueRequested">Raises Orchestrator's
    /// <c>MasterCueRequested</c> event — App relays it to the audio engine.</param>
    public static void Add(Dictionary<string, Action<Gesture>> map, IApplication app,
        Action<int> playPressed, Action<bool> masterCueRequested)
    {
        map[GestureIds.PlayPress] = g => playPressed(g.Deck);

        // Deliberately nothing. See DdjFlx4Mapping's note on why the old
        // beatgrid re-anchor binding was removed.
        map[GestureIds.CueTransportPlain] = _ => { };

        map[GestureIds.CueTransportRestart] = g => app.DeckFor(g.Deck).Player.SeekToFraction(0);

        map[GestureIds.CueHeadphoneToggle] = g =>
            app.DeckFor(g.Deck).CueActive = ((ControllerEvent.CueChanged)g.Source).On;

        map[GestureIds.MasterCueToggle] = g =>
            masterCueRequested(((ControllerEvent.MasterCueChanged)g.Source).On);

        // Beat sync is not implemented yet.
        map[GestureIds.SyncPress] = _ => { };

        map[GestureIds.SyncCycleTempoRange] = g => app.DeckFor(g.Deck).CycleTempoRange();
    }
}

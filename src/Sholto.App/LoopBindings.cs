using Sholto.Controller;
using Sholto.Controller.Gestures;

namespace Sholto.App;

/// <summary>Gesture bindings for looping: beat loop toggle/halve/double, and the
/// grid-nudge pair. Extracted verbatim from <c>Orchestrator.HandleGesture</c> —
/// see <see cref="TransportBindings"/>'s doc for why. Depends only on
/// <see cref="IApplication"/> and <see cref="IGestureRecognizer"/>, so it is
/// testable with a fake deck and no real Orchestrator.</summary>
public static class LoopBindings
{
    public static void Add(Dictionary<string, Action<Gesture>> map, IApplication app, IGestureRecognizer recognizer)
    {
        map[GestureIds.BeatLoopToggle] = g =>
            app.DeckFor(g.Deck).Player.EnableBeatLoop(((ControllerEvent.BeatLoopToggle)g.Source).Bars);
        map[GestureIds.BeatLoopHalve] = g => app.DeckFor(g.Deck).Player.HalveLoop();
        map[GestureIds.BeatLoopDouble] = g => app.DeckFor(g.Deck).Player.DoubleLoop();

        map[GestureIds.GridNudgeBack] = g => NudgeGrid(app, recognizer, g);
        map[GestureIds.GridNudgeForward] = g => NudgeGrid(app, recognizer, g);
    }

    private static void NudgeGrid(IApplication app, IGestureRecognizer recognizer, Gesture g)
    {
        int beats = ((ControllerEvent.NudgeGrid)g.Source).Beats;
        if (g.Deck >= 0) { app.DeckFor(g.Deck).Player.NudgeGrid(beats); return; }
        // The BEAT arrows are one pair shared by both decks, so they arrive
        // deckless. Pick a deck: held Shift first, then whichever deck has a
        // loop running, then deck 0.
        int target;
        if (recognizer.IsShiftHeld(0)) target = 0;
        else if (recognizer.IsShiftHeld(1)) target = 1;
        else if (app.DeckFor(0).Player.ActiveLoop is not null) target = 0;
        else if (app.DeckFor(1).Player.ActiveLoop is not null) target = 1;
        else target = 0;
        app.DeckFor(target).Player.NudgeGrid(beats);
    }
}

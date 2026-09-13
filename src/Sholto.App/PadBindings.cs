using Sholto.Controller;
using Sholto.Controller.Gestures;

namespace Sholto.App;

/// <summary>Gesture bindings for the pads: the three stem-mute pads, the echo pad,
/// and the two pad-mode presses — plus the two modifier-hold ids (SHIFT,
/// stem-level), which (like the pad-mode presses) are state-only: the recognizer
/// holds the modifier state and the Controller already repaints the pad LEDs on a
/// page switch, so these have always been no-ops here. Extracted verbatim from
/// <c>Orchestrator.HandleGesture</c> — see <see cref="TransportBindings"/>'s doc for
/// why. Depends only on <see cref="IApplication"/> and <see cref="IControlSurface"/>,
/// so it is testable with a fake deck and no real Orchestrator.</summary>
public static class PadBindings
{
    public static void Add(Dictionary<string, Action<Gesture>> map, IApplication app, IControlSurface surface)
    {
        // All three stem pads share one body — which stem toggles is read from
        // the event's own Group, not from which gesture id fired (this matches
        // the original switch, whose three case labels fell through into one
        // shared block).
        map[GestureIds.PadStemDrums] = g => ToggleStem(app, surface, g);
        map[GestureIds.PadStemVocals] = g => ToggleStem(app, surface, g);
        map[GestureIds.PadStemInstrumental] = g => ToggleStem(app, surface, g);

        map[GestureIds.PadEcho] = g =>
        {
            var deckVm = app.DeckFor(g.Deck);
            deckVm.EchoActive = !deckVm.EchoActive;
            surface.SetEchoLight(g.Deck, deckVm.EchoActive);
        };

        // Roll is a hold, not a toggle: RollHold carries both edges. Press
        // engages, release disengages — driven through the generic SetParam
        // seam (no named method for the roll, unlike PadEcho above).
        map[GestureIds.PadRoll] = g =>
        {
            var rh = (ControllerEvent.RollHold)g.Source;
            app.DeckFor(g.Deck).Player.SetParam("roll", 0, rh.Pressed ? 1 : 0);
        };

        // State only. The recognizer holds the modifier state; the Controller
        // already repaints the pad LEDs on a page switch.
        map[GestureIds.ShiftHold] = _ => { };
        map[GestureIds.StemLevelHold] = _ => { };
        map[GestureIds.PadModeHotCue] = _ => { };
        map[GestureIds.PadModePadFx1] = _ => { };
    }

    private static void ToggleStem(IApplication app, IControlSurface surface, Gesture g)
    {
        var st = (ControllerEvent.StemToggle)g.Source;
        var deckVm = app.DeckFor(g.Deck);
        bool nextActive = st.Group switch
        {
            0 => !deckVm.DrumsActive,
            1 => !deckVm.VocalsActive,
            _ => !deckVm.InstrumentalActive,
        };
        switch (st.Group)
        {
            case 0: deckVm.DrumsActive        = nextActive; break;
            case 1: deckVm.VocalsActive       = nextActive; break;
            case 2: deckVm.InstrumentalActive = nextActive; break;
        }
        deckVm.Player.SetStemGroup(st.Group, nextActive);
        surface.SetPadLight(g.Deck, st.Group, nextActive);
    }
}

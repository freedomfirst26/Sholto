using Sholto.Controller;
using Sholto.Controller.Gestures;

namespace Sholto.App;

/// <summary>Gesture bindings for the browse knob: turn, short press (deliberately
/// a no-op), long-press hold (re-analyze), and LOAD 1/2. Extracted verbatim from
/// <c>Orchestrator.HandleGesture</c> — see <see cref="TransportBindings"/>'s doc for
/// why. <c>loadSelectedIntoDeck</c> and <c>reanalyzeHighlighted</c> stay as
/// Orchestrator callbacks rather than being inlined here: both touch the decoder
/// + async load/analysis pipeline Orchestrator owns, which is out of scope for
/// this pass. Depends only on <see cref="IApplication"/> plus those two
/// callbacks, so it is testable with a fake deck and no real Orchestrator.</summary>
public static class BrowseBindings
{
    public static void Add(Dictionary<string, Action<Gesture>> map, IApplication app,
        Action<int> loadSelectedIntoDeck, Action<string> reanalyzeHighlighted)
    {
        map[GestureIds.BrowseTurn] = g =>
            app.OnBrowseRotated(((ControllerEvent.BrowseRotated)g.Source).Delta);

        // Deliberately nothing. LOAD 1 / LOAD 2 do the loading.
        map[GestureIds.BrowsePressShort] = _ => { };

        map[GestureIds.BrowsePressHold] = _ => reanalyzeHighlighted("browse-hold");

        map[GestureIds.LoadPress] = g => loadSelectedIntoDeck(g.Deck);
    }
}

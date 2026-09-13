namespace Sholto.App;

/// <summary>The subset of real keyboard shortcuts that have a DDJ-FLX4 equivalent —
/// the test applied at each key was "if a DDJ-FLX4 button does the same thing, it is
/// an app gesture; otherwise it is UI chrome and stays in the view." Everything else
/// a key can do (search, dialogs, the tag editor, list navigation, …) has no
/// controller equivalent and is handled directly by <c>MainWindow</c> — it never
/// reaches this port.
///
/// <para>Mirrors <see cref="Sholto.Controller.IControlSurface"/>'s shape: the real
/// keyboard (<c>MainWindow</c>) raises <see cref="Action"/>, <see cref="Orchestrator"/>
/// subscribes and turns each event into the same app call an equivalent controller
/// gesture would make — see <c>Orchestrator</c>'s keyboard handler and its
/// <c>GestureIds.LoadPress</c>/<c>PlayPress</c> cases for the shared code.</para></summary>
public interface IKeyboard
{
    event Action<KeyboardEvent>? Action;
}

/// <summary>Which app-level shortcut fired, and which deck it targets (-1 when the
/// action picks its own target, as <see cref="OpenGridEdit"/> does).</summary>
public readonly record struct KeyboardEvent(KeyboardGesture Kind, int Deck);

public enum KeyboardGesture
{
    /// <summary>1 / 2 — load the highlighted library track into deck 0/1. Same
    /// intent as the FLX4's LOAD 1/LOAD 2, and the same target-picking as
    /// <c>GestureIds.LoadPress</c>.</summary>
    LoadSelected,

    /// <summary>P (+Shift for deck 2) — play/pause. Same intent as the FLX4's PLAY.</summary>
    Play,

    /// <summary>M (+Shift for deck 2) — drop a marker on the target deck.</summary>
    AddMarker,

    /// <summary>G — open the beatgrid tuning tool on whichever deck the grid edit
    /// targets (open editor > active loop > first loaded deck). No FLX4 control
    /// does this; it is grouped with the app gestures anyway per the keyboard split
    /// decision recorded in ~/Projects/sholto.md.</summary>
    OpenGridEdit,
}

namespace Sholto.Controller.Gestures;

/// <summary>Port onto <see cref="GestureRecognizer"/>, taken by
/// <see cref="Sholto.App.Orchestrator"/> in place of the concrete type — a
/// collaborator whose identity/config is decided once at bootstrap, not a value
/// built from this call's own data. <see cref="GestureRecognizer.BrowseHold"/> stays
/// static: it's a plain constant, not state.</summary>
public interface IGestureRecognizer
{
    bool IsShiftHeld(int deck);
    bool IsStemLevelHeld { get; }
    bool IsPlatterTouched(int deck);
    PadPage PageFor(int deck);

    /// <summary>Resolve one event. Returns null when the event carries no gesture —
    /// the raw presses the Controller consumes itself, and the browse press, whose
    /// meaning is not known until it is released or held long enough.</summary>
    Gesture? Recognize(ControllerEvent evt, DateTime nowUtc);

    IReadOnlyList<Gesture> Tick(DateTime nowUtc);
}

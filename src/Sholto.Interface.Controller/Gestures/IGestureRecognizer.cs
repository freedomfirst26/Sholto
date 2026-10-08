
namespace Sholto.Interface.Controller.Gestures;

/// <summary>Port onto <see cref="GestureRecognizer"/>, taken by
/// <c>PerformanceTick</c> in place of the concrete type — a
/// collaborator whose identity/config is decided once at bootstrap, not a value
/// built from this call's own data.</summary>
internal interface IGestureRecognizer
{
    bool IsShiftHeld(int deck);
    bool IsPlatterTouched(int deck);

    /// <summary>Resolve one event. Returns null when the event carries no gesture —
    /// the browse press, whose
    /// meaning is not known until it is released or held long enough.</summary>
    Gesture? Recognize(ControllerEvent evt, DateTime nowUtc);

    IReadOnlyList<Gesture> Tick(DateTime nowUtc);
}

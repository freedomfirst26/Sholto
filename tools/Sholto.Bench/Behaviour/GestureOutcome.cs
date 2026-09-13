namespace Sholto.Bench.Behaviour;

/// <summary>
/// What one "gesture" or "midi" scenario step did — the counterpart to
/// <see cref="InteractionOutcome"/> for the controller-input plane. Built the
/// same way: a small set of observable VM/deck fields snapshotted immediately
/// before and after the event travels the real
/// GestureRecognizer → GestureBus → Orchestrator pipeline, diffed, and reported
/// — so "the deck's position/volume/etc. didn't move" is visible in the output
/// rather than a script that silently did nothing.
/// </summary>
public sealed class GestureOutcome
{
    public required string Action { get; init; }              // "gesture" | "midi"
    public required string Description { get; init; }
    /// <summary>The resolved <c>ControllerEvent</c>, as text (e.g.
    /// "JogRotated { Deck = 0, Delta = 5, Source = TopPlatter }"). For "midi",
    /// null means the mapping had no translation for that note/CC — an unmapped
    /// wire number, exactly what it would mean on real hardware.</summary>
    public string? ControllerEvent { get; init; }
    public IReadOnlyDictionary<string, FieldChange> Changed { get; init; } =
        new Dictionary<string, FieldChange>();
}

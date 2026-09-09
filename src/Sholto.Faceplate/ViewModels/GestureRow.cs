namespace Sholto.Faceplate.ViewModels;

/// <summary>One way to use whatever is currently on screen: a gesture the selected
/// control owns, or one owned by another control that names the selected control as a
/// partner (so clicking SHIFT surfaces the chords it unlocks, even though none of them
/// belongs to SHIFT itself).</summary>
/// <param name="OwnerControlId">The control this gesture actually belongs to. Equals
/// the selected control's id for an owned gesture; some other control's id when this
/// row was pulled in because it names the selected control as a partner.</param>
/// <param name="Partners">Control ids you must also hold. Empty for a plain gesture.</param>
public sealed record GestureRow(
    string GestureId,
    string OwnerControlId,
    string Verb,
    string? Part,
    string Layer,
    string Result,
    bool Used,
    IReadOnlyList<string> Partners);

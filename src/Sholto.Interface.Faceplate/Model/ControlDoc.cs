namespace Sholto.Interface.Faceplate.Model;

/// <summary>A physical thing on the unit. One shape in the drawing.</summary>
/// <param name="Scope">"per-deck" if the unit has one on each deck, else "global".</param>
public sealed record ControlDoc(
    string Id,
    string Label,
    string Scope,
    string Summary,
    IReadOnlyList<GestureDoc> Gestures);

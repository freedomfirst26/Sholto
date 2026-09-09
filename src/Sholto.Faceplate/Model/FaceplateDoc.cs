namespace Sholto.Faceplate.Model;

/// <summary>The words for one controller. Loaded from JSON.
/// <para>This file describes; it never resolves. It carries no event names and no
/// match rules — <see cref="Sholto.Controller.Gestures.GestureRecognizer"/> owns all
/// of that. The gesture id is the only join between the two.</para></summary>
public sealed record FaceplateDoc(
    string Device,
    IReadOnlyList<LayerDoc> Layers,
    IReadOnlyList<ControlDoc> Controls);

/// <summary>A way of reading the whole board. "Plain" is the board with nothing held;
/// the others are named after the modifier that produces them.</summary>
/// <param name="Modifier">The control you hold to enter this layer, or null for plain.</param>
public sealed record LayerDoc(string Id, string Name, string? Modifier = null);

/// <summary>A physical thing on the unit. One shape in the drawing.</summary>
/// <param name="Scope">"per-deck" if the unit has one on each deck, else "global".</param>
public sealed record ControlDoc(
    string Id,
    string Label,
    string Scope,
    string Summary,
    IReadOnlyList<GestureDoc> Gestures);

/// <summary>One way of using a control, and the one thing that results.</summary>
/// <param name="Id">Must be a value from GestureIds. The join to the code.</param>
/// <param name="Verb">What you do: "turn", "press", "hold", "touch, then turn".</param>
/// <param name="Part">Which part of the control, if it has more than one.</param>
/// <param name="Layer">The layer this gesture belongs to.</param>
/// <param name="Used">False when Sholto does nothing with it. Implemented controls get
/// a soft amber tint; unimplemented ones are drawn with no colour at all — the absence
/// is the signal, not a warning colour.</param>
/// <param name="With">Controls you must also hold. Drives the partner highlight
/// and the numbered steps.</param>
public sealed record GestureDoc(
    string Id,
    string Verb,
    string Layer,
    string Result,
    bool Used = true,
    string? Part = null,
    IReadOnlyList<string>? With = null);

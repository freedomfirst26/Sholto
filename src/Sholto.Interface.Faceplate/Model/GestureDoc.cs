namespace Sholto.Interface.Faceplate.Model;

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

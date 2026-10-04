namespace Sholto.App.Audio;

/// <summary>
/// Static metadata describing one controllable parameter of a
/// <see cref="DeckEffect"/>: its id, display name, range, default, and a hint
/// for how a control should present it. Pure data — no behaviour.
/// <see cref="DeckEffect.SetParam"/> is what actually applies a value; this
/// struct only describes it.
/// </summary>
public readonly record struct EffectParam(int Id, string Name, double Min, double Max, double Default, EffectParamCurve Curve);

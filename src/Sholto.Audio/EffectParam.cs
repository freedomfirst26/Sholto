namespace Sholto.Audio;

/// <summary>
/// Hints how a parameter's raw <c>double</c> value relates to what a
/// human-facing control should show. Purely descriptive — consulted by UI/MIDI
/// code when building a knob or label, never by any DSP path.
/// </summary>
public enum EffectParamCurve
{
    /// <summary>Value maps linearly onto [<see cref="EffectParam.Min"/>, <see cref="EffectParam.Max"/>].</summary>
    Linear,

    /// <summary>Value maps logarithmically onto [<see cref="EffectParam.Min"/>, <see cref="EffectParam.Max"/>]
    /// (e.g. frequency-like controls).</summary>
    Logarithmic,

    /// <summary>Value is 0/1 acting as a boolean toggle.</summary>
    Boolean,
}

/// <summary>
/// Static metadata describing one controllable parameter of a
/// <see cref="DeckEffect"/>: its id, display name, range, default, and a hint
/// for how a control should present it. Pure data — no behaviour.
/// <see cref="DeckEffect.SetParam"/> is what actually applies a value; this
/// struct only describes it.
/// </summary>
public readonly record struct EffectParam(int Id, string Name, double Min, double Max, double Default, EffectParamCurve Curve);

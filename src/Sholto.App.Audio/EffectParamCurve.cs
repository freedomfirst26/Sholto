namespace Sholto.App.Audio;

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

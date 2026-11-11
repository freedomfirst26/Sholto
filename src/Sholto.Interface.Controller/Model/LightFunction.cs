namespace Sholto.Interface.Controller.Model;

/// <summary>A lightable function on a controller. Logical — the per-device
/// mapping (IControllerMapping.RenderLight) turns it into actual MIDI bytes.</summary>
public enum LightFunction
{
    Cue,
    MasterCue,
    BeatSync,
    /// <summary>A pad-mode button on a deck (HOT CUE, PAD FX1, BEAT JUMP or SAMPLER).
    /// <see cref="ControllerLight.Index"/> is the page, <c>(int)PadPage</c>.</summary>
    PadMode,
}

namespace Sholto.Interface.Controller;

/// <summary>A lightable function on a controller. Logical — the per-device
/// mapping (IControllerMapping.RenderLight) turns it into actual MIDI bytes.</summary>
public enum LightFunction
{
    Cue,
    MasterCue,
    BeatSync,
    /// <summary>One of the 8 hot-cue pads on a deck (Hot Cue page).
    /// <see cref="ControllerLight.Pad"/> selects which pad (0-7).</summary>
    Pad,
    /// <summary>One of the 8 pads on a deck's PAD FX1 page. <see cref="ControllerLight.Pad"/>
    /// selects which pad (0-7) — only pad 0 (echo toggle) is used today.</summary>
    PadFx1,
    /// <summary>The HOT CUE pad-mode button on a deck.</summary>
    PadModeHotCue,
    /// <summary>The PAD FX1 pad-mode button on a deck.</summary>
    PadModePadFx1,
}

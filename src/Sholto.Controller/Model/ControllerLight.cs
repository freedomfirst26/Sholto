namespace Sholto.Controller;

/// <summary>A lightable function on a controller. Logical — the per-device
/// mapping (IControllerMapping.RenderLight) turns it into actual MIDI bytes.</summary>
public enum LightFunction
{
    Cue,
    MasterCue,
    BeatSync,
    /// <summary>One of the 8 hot-cue pads on a deck. <see cref="ControllerLight.Pad"/>
    /// selects which pad (0-7).</summary>
    Pad,
}

/// <summary>Addresses one lightable control: which deck (0 = Deck 1, 1 = Deck 2;
/// ignored for device-wide lights like MasterCue) and which function. Used as a
/// Button's identity when it asks the mapping to render its LED.
/// <paramref name="Pad"/> only matters for <see cref="LightFunction.Pad"/> — which
/// of the deck's 8 pads (0-7) this is.</summary>
public readonly record struct ControllerLight(int Deck, LightFunction Function, int Pad = 0);

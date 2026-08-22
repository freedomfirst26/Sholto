namespace Sholto.Controller;

/// <summary>A lightable function on a controller. Logical — the per-device
/// mapping (IControllerMapping.RenderLight) turns it into actual MIDI bytes.</summary>
public enum LightFunction
{
    Cue,
    MasterCue,
    BeatSync,
}

/// <summary>Addresses one lightable control: which deck (0 = Deck 1, 1 = Deck 2;
/// ignored for device-wide lights like MasterCue) and which function. Used as a
/// Button's identity when it asks the mapping to render its LED.</summary>
public readonly record struct ControllerLight(int Deck, LightFunction Function);

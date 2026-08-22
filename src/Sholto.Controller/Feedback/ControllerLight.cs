namespace Sholto.Controller;

/// <summary>A lightable function on a controller. Logical — the per-device
/// mapping turns it into actual MIDI bytes (see IControllerMapping.RenderLight).</summary>
public enum LightFunction
{
    Cue,
    Play,
    Loop,
}

/// <summary>Addresses one lightable control: which deck (0 = Deck 1, 1 = Deck 2)
/// and which function. This is the abstract "button" the app reasons about,
/// independent of any controller's MIDI numbers.</summary>
public readonly record struct ControllerLight(int Deck, LightFunction Function);

/// <summary>A light changed state — the feedback event broadcast on the bus.</summary>
public readonly record struct LightChanged(ControllerLight Light, bool On);

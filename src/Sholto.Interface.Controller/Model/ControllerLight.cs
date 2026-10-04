namespace Sholto.Interface.Controller;

/// <summary>Addresses one lightable control: which deck (0 = Deck 1, 1 = Deck 2;
/// ignored for device-wide lights like MasterCue) and which function. Used as a
/// Button's identity when it asks the mapping to render its LED.
/// <paramref name="Pad"/> only matters for <see cref="LightFunction.Pad"/> — which
/// of the deck's 8 pads (0-7) this is.</summary>
public readonly record struct ControllerLight(int Deck, LightFunction Function, int Pad = 0);

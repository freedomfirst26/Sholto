namespace Sholto.Interface.Controller.Model;

/// <summary>Addresses one lightable control: which deck (0 = Deck 1, 1 = Deck 2;
/// ignored for device-wide lights like MasterCue) and which function. Used as a
/// Button's identity when it asks the mapping to render its LED.
/// <paramref name="Index"/> only matters for <see cref="LightFunction.PadMode"/> — the pad page,
/// <c>(int)PadPage</c>, whose mode button this is.</summary>
public readonly record struct ControllerLight(int Deck, LightFunction Function, int Index = 0);

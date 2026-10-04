namespace Sholto.Data;

/// <summary>Halve, double, toggle or reset a deck's BPM override; the App persists it per track.</summary>
public readonly record struct ChangeBpmMultiplier(int Deck, BpmMultiplierOp Op, Origin Origin) : ICommand;

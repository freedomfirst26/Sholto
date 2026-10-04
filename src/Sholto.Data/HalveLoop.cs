namespace Sholto.Data;

/// <summary>Halve a deck's active loop.</summary>
public readonly record struct HalveLoop(int Deck, Origin Origin) : ICommand;

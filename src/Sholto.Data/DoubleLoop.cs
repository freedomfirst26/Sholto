namespace Sholto.Data;

/// <summary>Double a deck's active loop.</summary>
public readonly record struct DoubleLoop(int Deck, Origin Origin) : ICommand;

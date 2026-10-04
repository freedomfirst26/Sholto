namespace Sholto.Data;

/// <summary>Load the highlighted library track into a deck.</summary>
public readonly record struct LoadSelectedIntoDeck(int Deck, Origin Origin) : ICommand;

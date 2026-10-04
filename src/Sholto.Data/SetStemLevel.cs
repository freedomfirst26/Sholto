namespace Sholto.Data;

/// <summary>One stem's level on a deck, 0 to 1. <see cref="Stem"/> is 0 = drums, 1 = vocals, 2 = instrumental.</summary>
public readonly record struct SetStemLevel(int Deck, int Stem, double Value, Origin Origin) : ICommand;

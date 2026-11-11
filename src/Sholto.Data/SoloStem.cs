namespace Sholto.Data;

/// <summary>Solo one stem of a deck (<see cref="Stem"/>: 0 = drums, 1 = vocals, 2 = instrumental). Soloing the already-soloed stem brings all three back.</summary>
public readonly record struct SoloStem(int Deck, int Stem, Origin Origin) : ICommand;

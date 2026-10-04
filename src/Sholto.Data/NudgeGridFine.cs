namespace Sholto.Data;

/// <summary>Move a deck's beatgrid by <see cref="Seconds"/> (the tune editor's phase buttons and arrow keys).</summary>
public readonly record struct NudgeGridFine(int Deck, double Seconds, Origin Origin) : ICommand;

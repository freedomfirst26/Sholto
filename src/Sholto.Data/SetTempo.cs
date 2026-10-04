namespace Sholto.Data;

/// <summary>A deck's tempo fader, 0 to 1 with 0.5 neutral.</summary>
public readonly record struct SetTempo(int Deck, double Position, Origin Origin) : ICommand;

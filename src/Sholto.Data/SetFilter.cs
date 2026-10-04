namespace Sholto.Data;

/// <summary>A deck's filter knob, 0 to 1 with 0.5 bypass.</summary>
public readonly record struct SetFilter(int Deck, double Position, Origin Origin) : ICommand;

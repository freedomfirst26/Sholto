namespace Sholto.Data;

/// <summary>Beat-repeat roll on a deck: engaged while <see cref="Pressed"/>, released when it goes false.</summary>
public readonly record struct HoldRoll(int Deck, bool Pressed, Origin Origin) : ICommand;

namespace Sholto.Data;

/// <summary>One EQ band of a deck, 0 to 1 with 0.5 neutral. <see cref="Band"/> is 0 = low, 1 = mid, 2 = high.</summary>
public readonly record struct SetEq(int Deck, int Band, double Value, Origin Origin) : ICommand;

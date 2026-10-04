namespace Sholto.Data;

/// <summary>A marker was dropped on a deck at <see cref="Seconds"/> (a toast-worthy fact).</summary>
public readonly record struct MarkerAdded(int Deck, double Seconds) : IEvent;

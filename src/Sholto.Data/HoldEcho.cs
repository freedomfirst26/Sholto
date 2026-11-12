namespace Sholto.Data;

/// <summary>Beat-synced echo on a deck: on while <see cref="Pressed"/>; going false stops feeding the echo and
/// lets its tail ring out.</summary>
public readonly record struct HoldEcho(int Deck, bool Pressed, Origin Origin) : ICommand;

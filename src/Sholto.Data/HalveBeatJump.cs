namespace Sholto.Data;

/// <summary>Halve the jump of every BEAT JUMP pad on a deck (down to the App's smallest size).</summary>
public readonly record struct HalveBeatJump(int Deck, Origin Origin) : ICommand;

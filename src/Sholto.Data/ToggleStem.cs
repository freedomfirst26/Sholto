namespace Sholto.Data;

/// <summary>Mute or unmute one stem of a deck. <see cref="Stem"/> is 0 = drums, 1 = vocals, 2 = instrumental.</summary>
public readonly record struct ToggleStem(int Deck, int Stem, Origin Origin) : ICommand;

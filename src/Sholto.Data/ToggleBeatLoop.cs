namespace Sholto.Data;

/// <summary>Engage a <see cref="Bars"/>-bar loop on a deck, or exit the loop that is running.</summary>
public readonly record struct ToggleBeatLoop(int Deck, int Bars, Origin Origin) : ICommand;

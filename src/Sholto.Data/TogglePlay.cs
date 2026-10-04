namespace Sholto.Data;

/// <summary>Play or pause a deck (a play press, or the keyboard's P).</summary>
public readonly record struct TogglePlay(int Deck, Origin Origin) : ICommand;

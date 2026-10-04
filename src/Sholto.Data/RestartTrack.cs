namespace Sholto.Data;

/// <summary>Jump a deck back to the start of its track (Shift + CUE).</summary>
public readonly record struct RestartTrack(int Deck, Origin Origin) : ICommand;

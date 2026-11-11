namespace Sholto.Data;

/// <summary>Delete the hot cue on a pad (0-7). Nothing happens when the pad is empty.</summary>
public readonly record struct DeleteHotCue(int Deck, int Pad, Origin Origin) : ICommand;

namespace Sholto.Data;

/// <summary>A hot-cue pad (0-7) went down or up: set an empty pad, jump to a saved one.</summary>
public readonly record struct PressHotCue(int Deck, int Pad, bool Pressed, Origin Origin) : ICommand;

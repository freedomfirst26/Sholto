namespace Sholto.Data;

/// <summary>Flip a deck's headphone cue.</summary>
public readonly record struct ToggleHeadphoneCue(int Deck, Origin Origin) : ICommand;

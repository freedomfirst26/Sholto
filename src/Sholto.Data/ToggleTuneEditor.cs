namespace Sholto.Data;

/// <summary>Open the tune editor on a deck, or close it if it is open (a click on the BPM).</summary>
public readonly record struct ToggleTuneEditor(int Deck, Origin Origin) : ICommand;

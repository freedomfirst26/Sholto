namespace Sholto.Data;

/// <summary>Close the tune editor on a deck.</summary>
public readonly record struct CloseTuneEditor(int Deck, Origin Origin) : ICommand;

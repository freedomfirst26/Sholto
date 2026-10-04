namespace Sholto.Data;

/// <summary>Drop a marker on a deck at its current position.</summary>
public readonly record struct AddMarker(int Deck, Origin Origin) : ICommand;

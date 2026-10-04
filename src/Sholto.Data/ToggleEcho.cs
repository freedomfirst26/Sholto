namespace Sholto.Data;

/// <summary>Toggle a deck's beat-synced echo.</summary>
public readonly record struct ToggleEcho(int Deck, Origin Origin) : ICommand;

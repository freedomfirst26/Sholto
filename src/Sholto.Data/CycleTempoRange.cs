namespace Sholto.Data;

/// <summary>Step a deck's tempo-fader range (6, 10, 16, wide).</summary>
public readonly record struct CycleTempoRange(int Deck, Origin Origin) : ICommand;

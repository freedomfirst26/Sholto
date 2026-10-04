namespace Sholto.Data;

/// <summary>A waveform click at <see cref="Seconds"/> while the deck's grid-edit mode is on (anchor A, then the grid).</summary>
public readonly record struct ClickGrid(int Deck, double Seconds, Origin Origin) : ICommand;

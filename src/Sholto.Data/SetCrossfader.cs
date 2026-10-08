namespace Sholto.Data;

/// <summary>Crossfader position, 0 (full deck 1) to 1 (full deck 2).</summary>
public readonly record struct SetCrossfader(double Position, Origin Origin) : ICommand;

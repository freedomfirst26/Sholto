namespace Sholto.Data;

/// <summary>Move a deck's BPM by <see cref="Delta"/> (the tune editor's BPM steppers and arrow keys).</summary>
public readonly record struct AdjustBpm(int Deck, double Delta, Origin Origin) : ICommand;

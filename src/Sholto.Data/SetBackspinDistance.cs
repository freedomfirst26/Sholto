namespace Sholto.Data;

/// <summary>Set how far a firm fling rewinds the track, in beats of the loaded track: 0 = no coast (the deck
/// stops dead and resumes where the hand left). The App clamps it to <see cref="Min"/>..<see cref="Max"/>
/// (NaN becomes <see cref="Default"/>), applies it to the next fling and remembers it for the next launch.</summary>
public readonly record struct SetBackspinDistance(double Beats, Origin Origin) : ICommand
{
    /// <summary>No coast at all.</summary>
    public const double Min = 0.0;

    /// <summary>The furthest rewind offered, in beats.</summary>
    public const double Max = 16.0;

    /// <summary>The stock rewind, in beats (half a bar).</summary>
    public const double Default = 2.0;
}

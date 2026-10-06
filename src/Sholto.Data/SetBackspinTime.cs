namespace Sholto.Data;

/// <summary>Set how long a flung platter keeps spinning after the hand lets go, in seconds: 0 = no coast (the
/// deck stops dead and resumes where the hand left). The App clamps it to <see cref="Min"/>..<see cref="Max"/>
/// (NaN becomes <see cref="Default"/>), applies it to the next fling and remembers it for the next launch.</summary>
public readonly record struct SetBackspinTime(double Seconds, Origin Origin) : ICommand
{
    /// <summary>No coast at all.</summary>
    public const double Min = 0.0;

    /// <summary>The longest coast offered, in seconds.</summary>
    public const double Max = 3.0;

    /// <summary>The stock coast, in seconds.</summary>
    public const double Default = 0.6;

    /// <summary>Not a deck command: it applies to both platters.</summary>
    public int Deck => -1;
}

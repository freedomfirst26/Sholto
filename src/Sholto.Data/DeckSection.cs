namespace Sholto.Data;

/// <summary>One section of a deck's track, positioned in whole bars counted from the grid's first downbeat.
/// Times come from <see cref="DeckSectionsChanged"/>'s bar grid.</summary>
public readonly record struct DeckSection(DeckSectionKind Kind, int StartBar, int Bars)
{
    /// <summary>Index one past the last bar.</summary>
    public int EndBar => StartBar + Bars;
}

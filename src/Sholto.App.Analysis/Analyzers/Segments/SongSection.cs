namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>One section of a track, positioned in whole bars. Holds only the bar
/// position; times are derived from a <see cref="Sholto.App.Analysis.Analyzers.Beats.Beatgrid"/>
/// by <see cref="SongSectionExtensions"/>, so a grid nudge moves sections with it.</summary>
/// <param name="StartBar">Index of the first bar, counted from the grid's first downbeat.</param>
/// <param name="Bars">Length in bars.</param>
public readonly record struct SongSection(SectionKind Kind, int StartBar, int Bars)
{
    /// <summary>Index one past the last bar.</summary>
    public int EndBar => StartBar + Bars;
}

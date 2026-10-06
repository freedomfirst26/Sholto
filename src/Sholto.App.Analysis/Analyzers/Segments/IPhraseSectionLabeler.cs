namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>Gives structural labels to already-cut sections from their per-bar features.</summary>
public interface IPhraseSectionLabeler
{
    /// <summary>Returns <paramref name="spans"/> (their Kind is ignored) with kinds assigned.</summary>
    IReadOnlyList<SongSection> Label(BarFeatures features, IReadOnlyList<SongSection> spans);
}

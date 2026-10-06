using Sholto.App.Analysis.Analyzers;

namespace Sholto.App.Decks;

/// <summary>Builds the <see cref="SectionLayout"/> of a track from its basic analysis.</summary>
public interface ISectionLayoutFactory
{
    /// <summary>Recomputes the phrase grid and sections from the peaks and the grid in
    /// <paramref name="basic"/>. An empty grid gives no sections.</summary>
    SectionLayout Create(BasicAnalysis basic);

    /// <summary>The layout of a deck with no track: an empty grid, a default phrase grid, no sections.</summary>
    SectionLayout Empty();
}

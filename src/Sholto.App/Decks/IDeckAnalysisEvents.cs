using Sholto.App.Analysis.Analyzers.Segments;

namespace Sholto.App.Decks;

/// <summary>A deck's view of the analysis landing on its track: it listens to the per-type events of the
/// loaded track's analysis, raises the matching <see cref="DeckChange"/>s, and keeps the phrase-aware
/// section layout the basic analysis produces.</summary>
public interface IDeckAnalysisEvents
{
    /// <summary>Raised on the app thread when an analysis type lands (and when the sections change).</summary>
    event Action<DeckChange>? Changed;

    /// <summary>The phrase grid, sections and bar grid for the current track; empty until basic analysis lands.</summary>
    SectionLayout Layout { get; }

    /// <summary>Follow the loading port's current analysis, detaching the one followed before. Call at
    /// start-up and after each load, because the player swaps in a fresh analysis per track.</summary>
    void Rebind();

    /// <summary>Drop the section layout (a new track is loading) and raise <see cref="DeckChange.Sections"/>.</summary>
    void ClearLayout();
}

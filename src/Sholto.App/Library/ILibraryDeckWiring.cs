using Sholto.App.Analysis.Stores;

namespace Sholto.App.Library;

/// <summary>Routes deck, session and analysis-reporter events into the catalog's rows, and persists the
/// BPM multiplier a DJ chooses.</summary>
public interface ILibraryDeckWiring
{
    /// <summary>Raised on the app thread after a deck's analysis was applied to its row (or its path was not in the catalog).</summary>
    event Action? DeckAnalysisApplied;

    /// <summary>Start saving chosen BPM multipliers to <paramref name="store"/>.</summary>
    void AttachMultiplierStore(ITempoMultiplierStore store);
}

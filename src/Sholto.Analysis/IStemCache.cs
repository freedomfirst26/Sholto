namespace Sholto.Analysis;

/// <summary>
/// Narrow state query, separate from <see cref="IStemAnalysisStep"/>: "does this
/// track already have stems on disk", answered without ever triggering a demucs run
/// (a demucs run is 30-180 s per track — <c>MainViewModel.HydrateStemStateAsync</c>
/// asks this for every library row at startup to decide which tracks can use stem
/// controls, and must not accidentally provoke hundreds of runs doing it). This is
/// the honest home for the question <c>IStemStatus</c> used to answer before caching
/// was split out of the analysis step entirely (see <see cref="CachingStemAnalysisStep"/>) —
/// <c>IStemStatus</c> is gone, replaced by this.
///
/// Also the thing <see cref="CachingStemAnalysisStep"/> asks on the hot path: a hit
/// returns the cached <see cref="StemPaths"/> without ever calling the inner step.
/// Implemented by whichever component owns the on-disk cache layout for its analysis
/// type (<see cref="DemucsStemCache"/> for stems), kept as its own interface so a
/// caller that only needs the state query isn't handed the ability to kick off
/// analysis too.
/// </summary>
public interface IStemCache
{
    /// <summary>On-disk check: are the four stem WAVs already cached for this file?
    /// Pure filesystem hit, no file reads, never runs the separator.</summary>
    bool Contains(string filePath);

    /// <summary>The cached stems for this file, or null if they aren't cached yet.
    /// Same on-disk check as <see cref="Contains"/>; returns the paths in one call
    /// rather than making the caller re-derive them after a true <see cref="Contains"/>.</summary>
    StemPaths? TryGet(string filePath);
}

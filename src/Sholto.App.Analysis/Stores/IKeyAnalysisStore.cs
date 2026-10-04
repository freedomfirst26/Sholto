using Sholto.App.Analysis.Analyzers.Keys;

namespace Sholto.App.Analysis.Stores;

/// <summary>
/// Persistence port for one track's musical key. Lives here (not in
/// <c>Sholto.App.Audio</c>, where <c>Deck</c> — its only consumer — lives, and not in
/// <c>Sholto.App.Storage</c>, where the SQLite-backed implementation lives) following
/// the same shape as <see cref="IAnalysisCache"/>: both <c>Sholto.App.Audio</c> and
/// <c>Sholto.App.Storage</c> already reference <c>Sholto.App.Analysis</c>, so putting the
/// port here needs no new project reference in either direction — the alternative
/// (declaring it in <c>Sholto.App.Audio</c>) would force <c>Sholto.App.Storage</c>, a
/// persistence leaf, to reference the audio-engine project just to implement an
/// interface.
/// </summary>
public interface IKeyAnalysisStore
{
    Task<KeyAnalysis?> TryGetAsync(string filePath);
    Task PutAsync(string filePath, KeyAnalysis key);

    /// <summary>Every stored key, by track path, with no mtime check — for library
    /// hydration. The store is the only reader of key rows; rows that fail to
    /// decode are skipped.</summary>
    Task<IReadOnlyDictionary<string, KeyAnalysis>> GetAllAsync();
}

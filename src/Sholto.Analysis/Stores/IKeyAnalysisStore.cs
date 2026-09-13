using Sholto.Analysis.Analyzers.Keys;

namespace Sholto.Analysis.Stores;

/// <summary>
/// Persistence port for one track's musical key. Lives here (not in
/// <c>Sholto.Audio</c>, where <c>Deck</c> — its only consumer — lives, and not in
/// <c>Sholto.Storage</c>, where the SQLite-backed implementation lives) following
/// the same shape as <see cref="IAnalysisCache"/>: both <c>Sholto.Audio</c> and
/// <c>Sholto.Storage</c> already reference <c>Sholto.Analysis</c>, so putting the
/// port here needs no new project reference in either direction — the alternative
/// (declaring it in <c>Sholto.Audio</c>) would force <c>Sholto.Storage</c>, a
/// persistence leaf, to reference the audio-engine project just to implement an
/// interface.
/// </summary>
public interface IKeyAnalysisStore
{
    Task<KeyAnalysis?> TryGetAsync(string filePath);
    Task PutAsync(string filePath, KeyAnalysis key);
}

/// <summary>The do-nothing store — always a miss, writes go nowhere. Used while
/// the DB hasn't opened yet; see <see cref="SwitchableKeyAnalysisStore"/> for how
/// a real one takes over without anyone holding a null.</summary>
public sealed class NullKeyAnalysisStore : IKeyAnalysisStore
{
    public static readonly NullKeyAnalysisStore Instance = new();
    private NullKeyAnalysisStore() { }
    public Task<KeyAnalysis?> TryGetAsync(string filePath) => Task.FromResult<KeyAnalysis?>(null);
    public Task PutAsync(string filePath, KeyAnalysis key) => Task.CompletedTask;
}

/// <summary>
/// Forwards to whatever <see cref="IKeyAnalysisStore"/> is currently attached —
/// <see cref="NullKeyAnalysisStore"/> until <see cref="Attach"/> is called once.
///
/// Exists for exactly one reason: the app window paints its first frame (and
/// <c>DeckFactory</c> builds both decks) before the DB opens, so the real,
/// SQLite-backed cache genuinely does not exist yet at the moment a Deck needs
/// one handed to its constructor. Deck's cache hook is a constructor parameter
/// (see <c>DeckFactory</c>/<c>Deck</c>) — immutable once built — so the deck
/// itself can never be "finished later". This object is the one thing that IS
/// mutable: both decks are handed the SAME instance up front, it starts
/// delegating to the null object, and once the DB opens the composition root
/// calls <see cref="Attach"/> once. From that point every already-constructed
/// Deck's next call lands on the real cache — nobody swaps a reference on Deck,
/// Deck never even knows a swap happened.
/// </summary>
public sealed class SwitchableKeyAnalysisStore : IKeyAnalysisStore
{
    private IKeyAnalysisStore _target = NullKeyAnalysisStore.Instance;

    /// <summary>Point at the real cache. Call once, after the DB opens.</summary>
    public void Attach(IKeyAnalysisStore real) => _target = real;

    public Task<KeyAnalysis?> TryGetAsync(string filePath) => _target.TryGetAsync(filePath);
    public Task PutAsync(string filePath, KeyAnalysis key) => _target.PutAsync(filePath, key);
}

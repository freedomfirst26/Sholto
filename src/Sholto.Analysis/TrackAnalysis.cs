namespace Sholto.Analysis;

/// <summary>
/// Collection of analyses attached to a track. Each concrete IAnalysis type is
/// stored at most once (keyed by Type). Always-optional — a fresh track may
/// have no analyses yet; a fully-analyzed track has Basic + Key + …
/// </summary>
public sealed class TrackAnalysis
{
    private readonly Dictionary<Type, IAnalysis> _byType = new();

    public IReadOnlyCollection<IAnalysis> All => _byType.Values;

    public T? Get<T>() where T : class, IAnalysis =>
        _byType.TryGetValue(typeof(T), out var a) ? (T)a : null;

    public bool Has<T>() where T : IAnalysis => _byType.ContainsKey(typeof(T));

    public void Set<T>(T analysis) where T : class, IAnalysis =>
        _byType[typeof(T)] = analysis;

    /// <summary>Convenience accessor for the most common case.</summary>
    public BasicAnalysis? Basic => Get<BasicAnalysis>();
}

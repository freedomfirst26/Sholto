namespace Sholto.Analysis;

/// <summary>
/// Collection of analyses attached to a track. Each concrete IAnalysis type is
/// stored at most once (keyed by Type). Always-optional — a fresh track may
/// have no analyses yet; a fully-analyzed track has Basic + Key + …
///
/// Raises per-type events when an analysis lands so listeners can subscribe
/// to exactly the signal they care about. For example: the deck view model
/// listens to <see cref="BasicReady"/> to unlock BPM display and magnetism,
/// and to <see cref="KeyReady"/> to reveal the key chip. There's also an
/// <see cref="AnyReady"/> event for code that wants the union signal.
/// </summary>
public sealed class TrackAnalysis
{
    private readonly Dictionary<Type, IAnalysis> _byType = new();

    /// <summary>Fires when any analysis is set on this track, after the typed
    /// event for that specific type. Useful for "something landed, re-check
    /// everything" handlers.</summary>
    public event Action<IAnalysis>? AnyReady;

    /// <summary>Fires when basic analysis (BPM, beat times, downbeats, waveform
    /// peaks) is set. Listeners can then enable BPM display, beat grid, and
    /// magnetism features.</summary>
    public event Action<BasicAnalysis>? BasicReady;

    /// <summary>Fires when key analysis is set. Listeners can then reveal the
    /// Camelot key chip and enable harmonic mixing helpers.</summary>
    public event Action<KeyAnalysis>? KeyReady;

    /// <summary>Fires when Demucs stems become available (cached path landed).
    /// Listeners can then surface per-stem mute controls.</summary>
    public event Action<StemPaths>? StemsReady;

    /// <summary>Fires when per-stem waveform peaks have been computed (some time
    /// after StemsReady, since computing peaks for 4 buffers takes a moment).
    /// The deck view model re-emits its Peaks binding so the waveform redraws
    /// using the new per-stem-aware merge.</summary>
    public event Action<StemPeaks>? StemPeaksReady;

    public IReadOnlyCollection<IAnalysis> All => _byType.Values;

    public T? Get<T>() where T : class, IAnalysis =>
        _byType.TryGetValue(typeof(T), out var a) ? (T)a : null;

    public bool Has<T>() where T : IAnalysis => _byType.ContainsKey(typeof(T));

    public void Set<T>(T analysis) where T : class, IAnalysis
    {
        _byType[typeof(T)] = analysis;

        // Fire the typed event before the generic one so per-type handlers see
        // the new state first. Subscribers run on the analyser thread — UI
        // consumers should marshal to the dispatcher themselves.
        switch (analysis)
        {
            case BasicAnalysis b: BasicReady?.Invoke(b);     break;
            case KeyAnalysis k:   KeyReady?.Invoke(k);       break;
            case StemPaths s:     StemsReady?.Invoke(s);     break;
            case StemPeaks p:     StemPeaksReady?.Invoke(p); break;
        }
        AnyReady?.Invoke(analysis);
    }

    /// <summary>Convenience accessor for the most common case.</summary>
    public BasicAnalysis? Basic => Get<BasicAnalysis>();
}

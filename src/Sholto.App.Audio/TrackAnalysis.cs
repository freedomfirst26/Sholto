using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Analyzers.Vocals;

namespace Sholto.App.Audio;

/// <summary>
/// Collection of analyses attached to a track. Each analysis result type is
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
    private readonly Dictionary<Type, object> _byType = new();

    /// <summary>Fires when any analysis is set on this track, after the typed
    /// event for that specific type. Useful for "something landed, re-check
    /// everything" handlers.</summary>
    public event Action<object>? AnyReady;

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

    /// <summary>Fires when the vocal-presence regions have been derived from the
    /// vocal stem (after StemsReady). The deck session re-raises its VocalRegions
    /// binding so the waveform paints the green "vocals here" rectangles.</summary>
    public event Action<IReadOnlyList<VocalRegion>>? VocalRegionsReady;

    public IReadOnlyCollection<object> All => _byType.Values;

    public T? Get<T>() where T : class =>
        _byType.TryGetValue(typeof(T), out var a) ? (T)a : null;

    public bool Has<T>() where T : class => _byType.ContainsKey(typeof(T));

    public void Set<T>(T analysis) where T : class
    {
        _byType[typeof(T)] = analysis;

        // Fire the typed event before the generic one so per-type handlers see
        // the new state first. Subscribers run on the analyser thread — UI
        // consumers should marshal to the dispatcher themselves.
        // Dispatch on the declared type (the dictionary key), not the runtime type,
        // so list-typed analyses (VocalRegions) route the same way they are keyed.
        if (typeof(T) == typeof(BasicAnalysis)) BasicReady?.Invoke((BasicAnalysis)(object)analysis);
        else if (typeof(T) == typeof(KeyAnalysis)) KeyReady?.Invoke((KeyAnalysis)(object)analysis);
        else if (typeof(T) == typeof(StemPaths)) StemsReady?.Invoke((StemPaths)(object)analysis);
        else if (typeof(T) == typeof(IReadOnlyList<VocalRegion>))
            VocalRegionsReady?.Invoke((IReadOnlyList<VocalRegion>)(object)analysis);
        AnyReady?.Invoke(analysis);
    }

    /// <summary>Convenience accessor for the most common case.</summary>
    public BasicAnalysis? Basic => Get<BasicAnalysis>();
}

using Sholto.Analysis;
using Sholto.Analysis.Processing;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Load a track onto a deck, tear it down again, and wire the SoundFlow graph
/// (mixer + post-mix effect chain) that carries it. Extracted out of
/// <see cref="Deck"/> — see <see cref="TrackLoading"/> for the implementation
/// and, in particular, why it owns the live <c>SoundPlayer</c>/data-provider
/// state that other Deck components only ever see through an accessor.
/// </summary>
public interface ITrackLoading
{
    /// <summary>Announce "a new track is about to load" — resets the in-memory
    /// analysis so stale waveform/BPM/key bindings clear right away, without
    /// waiting for <see cref="Load"/> (which can't run until samples are decoded).
    /// Audio for the previous track keeps playing until Load lands; this is purely
    /// a visual reset so the deck UI matches the new track immediately on click.</summary>
    void BeginLoad();

    /// <summary>
    /// Start playback from <paramref name="filePath"/> via SoundFlow's
    /// <c>ChunkedDataProvider</c>. Audio is playable within ~100 ms (file
    /// open + first chunk decode) regardless of track length. The full file is
    /// then decoded once in the background — solely for analysis (BPM, beats,
    /// key, waveform peaks). Analysis results land via the per-type
    /// <see cref="TrackAnalysis"/> events some seconds later.
    /// Memory footprint while playing: a couple of chunks worth of native
    /// PCM inside ChunkedDataProvider, no full mixed buffer.
    /// </summary>
    void LoadStreaming(string filePath);

    /// <summary>
    /// Synchronous load (audio starts immediately). Beat analysis is kicked off in
    /// the background; the AnalysisUpdated callback fires once it completes so the
    /// view model can re-bake the waveform with real beats.
    /// </summary>
    void Load(string filePath, float[] stereoSamples, int sampleRate);

    /// <summary>Eject the current track: stop playback, detach the SoundPlayer, clear analysis.</summary>
    void Unload();

    /// <summary>Build the deck's own mixer and the ordered post-mix effect
    /// chain (EQ → filter → echo) on top of it. Must be called once before
    /// <see cref="LoadStreaming"/>/<see cref="Load"/> will accept a track.</summary>
    void AttachEngine(SfEngine engine, AudioFormat format);

    /// <summary>
    /// Layered analysis cache (memory → db → compute). Constructor-injected —
    /// never null, never half-built.
    /// </summary>
    IAnalysisProvider AnalysisProvider { get; }

    /// <summary>
    /// Shared reporter — receives waveform / beats / stems progress events.
    /// Constructor-injected, same reasoning as <see cref="AnalysisProvider"/>.
    /// </summary>
    IAnalysisReporter Reporter { get; }

    /// <summary>Stem separation (demucs by default). Optional; playback just stays on
    /// the mixed track when the tool isn't installed.</summary>
    IStemAnalysisStep StemAnalyzer { get; }

    /// <summary>The current track's live analysis (peaks, BPM, key, stems, …).
    /// Reassigned to a fresh, empty instance by <see cref="BeginLoad"/>,
    /// <see cref="LoadStreaming"/>, <see cref="Load"/> and <see cref="Unload"/>;
    /// filled in place as each analysis stage lands.</summary>
    TrackAnalysis Analysis { get; }

    bool IsLoaded { get; }
    bool IsPlaying { get; }

    /// <summary>Raised on the analysis thread once an analysis stage completes,
    /// and by <see cref="BeginLoad"/>/<see cref="Unload"/> so the UI resets
    /// right away.</summary>
    event Action? AnalysisUpdated;
}

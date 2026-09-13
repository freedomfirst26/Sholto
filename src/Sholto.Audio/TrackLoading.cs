using Sholto.Analysis;
using Sholto.Analysis.Analyzers;
using Sholto.Analysis.Processing;
using Sholto.Analysis.Reporting;
using Sholto.Analysis.Stems;
using Sholto.Analysis.Analyzers.Waveform;
using SoundFlow.Abstracts;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Metadata.Models;
using SoundFlow.Modifiers;
using SoundFlow.Providers;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Track load/unload and the SoundFlow graph wiring underneath it, extracted
/// out of <see cref="Deck"/>. Named for what it does — put a track on a deck
/// and take it off again — rather than "Deck…" like the earlier extractions,
/// following <c>TransportControl</c>'s naming rather than
/// <c>DeckLooping</c>/<c>DeckBeatgrid</c>/<c>DeckMixer</c>/<c>DeckTempo</c>'s.
///
/// <para><b>What this owns.</b> Every field that <see cref="Load"/>,
/// <see cref="LoadStreaming"/> and <see cref="SwitchToStemMode"/> construct
/// and <see cref="TearDownPlayers"/> tears down: the engine/format handed to
/// <see cref="AttachEngine"/>, the deck's <c>Mixer</c>, the live
/// <c>SoundPlayer</c>, the current data provider (raw/chunked/stem), the
/// scratch-capable provider, the sample count/rate and current file path, and
/// the live <see cref="TrackAnalysis"/>. This is deliberately the reverse of
/// every sibling component (<c>DeckLooping</c>, <c>DeckBeatgrid</c>,
/// <c>DeckMixer</c>, <c>TransportControl</c>) — they take this state as a
/// <em>read-only accessor</em> because Deck used to be the one replacing it
/// on every load; now this class IS the one replacing it, so it owns the
/// fields outright and Deck (and, through Deck, every sibling) reads them
/// back off this instance's own properties instead.</para>
///
/// <para><b>What this writes back through instead of owning.</b> The
/// sibling components' internal-only reset hooks — <c>DeckBeatgrid.
/// ResetForNewTrack</c>/<c>SetDetectedBasic</c>, <c>DeckLooping.ExitLoop</c>,
/// <c>DeckMixer.ReapplyGain</c>/<c>AttachModifiers</c> — and the tempo
/// fader's current <c>PlaybackSpeed</c>, all handed in as narrow delegates
/// exactly like every other cross-component call in this codebase (no
/// back-reference to Deck or to a sibling's concrete type). The one piece of
/// state this writes back into <em>Deck itself</em> is the scratch flag: a
/// scratch mid-flight on the outgoing track has nothing left to drive once
/// <see cref="TearDownPlayers"/> runs, so it calls <c>resetScratching</c> —
/// Deck still owns <c>_scratching</c> because the scratch cluster (deliberately
/// untouched this stage) reads and writes it everywhere else.</para>
///
/// <para><b>Ordering is load-bearing.</b> Every sequence below — provider
/// built before assigned to <c>_player</c>, gain reapplied only after the new
/// player exists, the outgoing player/provider disposed before the new one is
/// wired in, <c>_scratchProvider</c>/<c>_stemProvider</c> nulled at the exact
/// point the old one stops being current — is copied unchanged from
/// <c>Deck</c>. The audio callback reads <c>_player</c>/the providers live;
/// reordering any of this is a correctness bug, not a style choice.</para>
///
/// <para><b>The echo/ResetControls bug.</b> A running echo used to survive a
/// track load and bleed the outgoing track's tail into the incoming one,
/// because <c>ResetControls</c> reset every other control but never flushed
/// the echo's delay line — fixed by adding <c>DeckMixer.ResetEcho</c> to it.
/// <c>ResetControls</c> is not part of this cluster (it stays on <c>Deck</c>,
/// calling <c>_loops.ExitLoop()</c>/<c>_mixer.ResetEcho()</c> directly) and
/// this class doesn't touch it — noted here only so nobody "simplifies" a
/// future load path by dropping that call.</para>
/// </summary>
internal sealed class TrackLoading : ITrackLoading
{
    private readonly IAudioFileDecoder _decoder;
    private readonly ITrackAnalysisRun _analysisRun;
    private readonly IReadOnlyList<Func<SfEngine, AudioFormat, SoundModifier>> _effectFactories;

    // Narrow write-backs into sibling components' internal-only members and
    // into Deck's own scratch flag — see the class doc's "writes back
    // through" section. None of these is a back-reference to Deck or to a
    // sibling's concrete type.
    private readonly Action _resetForNewTrack;
    private readonly Action _exitLoop;
    private readonly Action<IReadOnlyList<SoundModifier>> _attachModifiers;
    private readonly Action _reapplyGain;
    private readonly Func<float> _playbackSpeed;
    private readonly Action _resetScratching;

    public TrackLoading(
        IAudioFileDecoder decoder,
        ITrackAnalysisRun analysisRun,
        IReadOnlyList<Func<SfEngine, AudioFormat, SoundModifier>> effectFactories,
        Action resetForNewTrack,
        Action exitLoop,
        Action<IReadOnlyList<SoundModifier>> attachModifiers,
        Action reapplyGain,
        Func<float> playbackSpeed,
        Action resetScratching)
    {
        _decoder = decoder;
        _analysisRun = analysisRun;
        _effectFactories = effectFactories;
        _resetForNewTrack = resetForNewTrack;
        _exitLoop = exitLoop;
        _attachModifiers = attachModifiers;
        _reapplyGain = reapplyGain;
        _playbackSpeed = playbackSpeed;
        _resetScratching = resetScratching;

        // Relay analysis events exactly the way Deck relays this class's own
        // AnalysisUpdated (see Deck's constructor) — one hop further down the
        // same chain now that the analysis pipeline lives in its own class.
        _analysisRun.AnalysisUpdated += () => AnalysisUpdated?.Invoke();
    }

    /// <inheritdoc/>
    public IAnalysisProvider AnalysisProvider => _analysisRun.AnalysisProvider;
    /// <inheritdoc/>
    public IAnalysisReporter Reporter => _analysisRun.Reporter;
    /// <inheritdoc/>
    public IStemAnalysisStep StemAnalyzer => _analysisRun.StemAnalyzer;
    /// <inheritdoc/>
    public TrackAnalysis Analysis => _analysisRun.Analysis;

    private SfEngine? _engine;
    private AudioFormat _format;
    private Mixer? _deckMixer;
    private SoundPlayer? _player;
    // Strong reference to the SoundPlayer's current data provider so we can
    // Dispose() it explicitly when ejecting a track. SoundFlow's SoundPlayer
    // does not auto-dispose its provider, so without this the previous track's
    // float[] samples (RawDataProvider) or 4×float[] stems (StemMixDataProvider)
    // would survive until the next GC sweep, potentially holding hundreds of MB.
    private SoundFlow.Interfaces.ISoundDataProvider? _currentDataProvider;
    private int _sampleRate = 48000;
    private long _sampleCount;
    // Current track's file path — captured at Load / LoadStreaming so grid
    // adjustments can be persisted per-track via GridAdjustmentPut.
    private string? _currentFilePath;

    // Stem playback: when stems are available we swap the SoundPlayer's data provider
    // to a StemMixDataProvider that owns the 4 decoded stem buffers and mixes them
    // on demand. The audio path stays single-player; per-stem mute is just a
    // lock-free gain write inside that provider. No extra SoundPlayers, no extra
    // mixer summing, no extra resamplers.
    private StemMixDataProvider? _stemProvider;
    // In-memory scratch-capable provider for load path (2) — set in Load(),
    // replaced by _stemProvider once stems land (SwitchToStemMode tears it
    // down like any other previous-track provider). See ScratchDataProvider
    // for why this exists instead of SoundFlow's own RawDataProvider.
    private ScratchDataProvider? _scratchProvider;

    /// <inheritdoc/>
    public event Action? AnalysisUpdated;

    /// <inheritdoc/>
    public bool IsLoaded => _player is not null;
    /// <inheritdoc/>
    public bool IsPlaying => _player?.State == PlaybackState.Playing;

    // — Internal-only accessors. Not on ITrackLoading: these are the values
    // Deck's OTHER clusters (scratch, stems, transport-via-sibling-ctor) read
    // back now that this class owns them — narrow, live, read-only, exactly
    // like the accessor delegates every sibling component already takes, just
    // read the other direction because this class is the one that mutates the
    // underlying field. —

    internal SfEngine? Engine => _engine;
    internal AudioFormat Format => _format;
    internal Mixer? DeckMixer => _deckMixer;
    internal SoundPlayer? Player => _player;
    internal StemMixDataProvider? StemProvider => _stemProvider;
    internal ScratchDataProvider? ScratchProvider => _scratchProvider;
    internal long SampleCount => _sampleCount;
    internal string? CurrentFilePath => _currentFilePath;
    internal bool StemsLoaded => _stemProvider is not null;

    /// <summary>Write-back target for <c>TrackAnalysisRun</c>'s <c>setSampleCount</c>
    /// delegate — the exact sample count (line 278's old write) lands here once
    /// the streaming path's decode finishes.</summary>
    internal void SetSampleCount(long count) => _sampleCount = count;

    /// <inheritdoc/>
    public void AttachEngine(SfEngine engine, AudioFormat format)
    {
        _engine = engine;
        _format = format;
        _deckMixer = new Mixer(engine, format);

        // Build the post-mix chain from the ordered factory list handed in
        // at construction (see DeckEffectFactories) and add each modifier to
        // the deck's own mixer IN ORDER — SoundFlow's SoundComponent.Process
        // iterates its modifier array in add order, so this is what makes
        // EQ → filter → echo (today's order, and audibly not a free choice —
        // see the stage-1 design note) the order that runs. A single EQ/
        // filter/echo instance each processes the deck's already-summed
        // signal; one stateful modifier per stem would let the 4 streams
        // trample each other's filter state.
        var effects = new SoundModifier[_effectFactories.Count];
        for (int i = 0; i < _effectFactories.Count; i++)
        {
            var modifier = _effectFactories[i](engine, format);
            _deckMixer.AddModifier(modifier);
            effects[i] = modifier;
        }

        // Hand the built chain to DeckMixer — it can't construct these
        // itself (see its class doc) and picks out the ones it drives
        // (SetEq/SetFilter/SetEcho) by type.
        _attachModifiers(effects);
    }

    /// <inheritdoc/>
    public void BeginLoad()
    {
        _analysisRun.Analysis = new TrackAnalysis();
        // Fresh track: drop the previous track's detection + adjustment so a
        // late-arriving regen can't apply the old offset to the new grid. The
        // new track's saved adjustment (if any) is re-applied by the load path
        // via ApplyGridAdjustment once its detection lands.
        _resetForNewTrack();
        AnalysisUpdated?.Invoke();
    }

    /// <inheritdoc/>
    public void LoadStreaming(string filePath)
    {
        if (_engine is null || _deckMixer is null)
            throw new InvalidOperationException("AttachEngine must be called first.");

        _analysisRun.Analysis = new TrackAnalysis();

        TearDownPlayers();

        // ChunkedDataProvider owns the FileStream and disposes it as part of its
        // own Dispose. We pass minimal ReadOptions — no tag/album-art parsing
        // (track metadata already lives in the library scan).
        var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var provider = new ChunkedDataProvider(_engine, fileStream,
            new ReadOptions { ReadTags = false, ReadAlbumArt = false },
            chunkSize: 32768);

        _currentDataProvider = provider;
        _sampleRate = provider.SampleRate;
        _sampleCount = provider.Length > 0 ? provider.Length / 2 : 0;

        _player = new SoundPlayer(_engine, _format, provider);
        _deckMixer.AddComponent(_player);
        // Carry the channel fader's current attenuation forward — otherwise
        // SoundPlayer defaults to 1.0 and a deck loaded with the fader down
        // bursts in at full volume until the user touches the fader.
        _player.Volume = 1.0f; // unity — master gain applied downstream by the router (pre-fader cue tap)
        _reapplyGain();
        _currentFilePath = filePath;

        Console.WriteLine($"[Deck] streaming {Path.GetFileName(filePath)} @ {provider.SampleRate}Hz; engine={_format.SampleRate}Hz; length={provider.Length} samples");

        // BPM/key analysis for this track — see TrackAnalysisRun.KickOffAnalysisFor
        // for the decode-once-feed-both pipeline.
        _analysisRun.KickOffAnalysisFor(filePath);
    }

    /// <inheritdoc/>
    public void Load(string filePath, float[] stereoSamples, int sampleRate)
    {
        if (_engine is null || _deckMixer is null)
            throw new InvalidOperationException("AttachEngine must be called first.");

        _analysisRun.Analysis = new TrackAnalysis();
        _sampleRate = sampleRate;
        _sampleCount = stereoSamples.Length / 2;

        TearDownPlayers();

        // ScratchDataProvider, not SoundFlow's own RawDataProvider — its
        // varispeed support (signed speed, so the top platter can scratch a
        // track before stems land) is why it exists; see its class doc.
        var provider = new ScratchDataProvider(stereoSamples, sampleRate);
        _scratchProvider = provider;
        _currentDataProvider = provider;
        _player = new SoundPlayer(_engine, _format, provider);
        // Carry the channel fader's current attenuation forward — otherwise
        // SoundPlayer defaults to 1.0 and a deck loaded with the fader down
        // bursts in at full volume until the user touches the fader.
        _player.Volume = 1.0f; // unity — master gain applied downstream by the router (pre-fader cue tap)
        _reapplyGain();
        _currentFilePath = filePath;

        // EQ lives on _deckMixer (post-mix) — see AttachEngine. Don't attach here.
        _deckMixer.AddComponent(_player);
        // Speed is owned by the provider (see ApplyPlaybackSpeed / ScratchRate),
        // never by SoundFlow.SoundPlayer.PlaybackSpeed (WSOLA, chops the UI).
        provider.SetSpeed(_playbackSpeed());
        Console.WriteLine($"[Deck] loaded {stereoSamples.Length} samples @ {sampleRate}Hz; engine={_format.SampleRate}Hz {_format.Channels}ch {_format.Format}");

        // Analysis runs off-thread; deck plays immediately, beat grid appears when
        // ready. See TrackAnalysisRun.KickOffBasicAnalysis.
        var decodedTrack = new DecodedTrack(filePath, stereoSamples, sampleRate, AudioFileDecoder.TargetChannels);
        _analysisRun.KickOffBasicAnalysis(decodedTrack);

        // Key estimation is independent of beats and stems — reads the same decoded
        // buffer the basic analysis used. See TrackAnalysisRun.KickOffKeyAnalysis.
        _analysisRun.KickOffKeyAnalysis(decodedTrack);

        // Stems run independently of the BPM pipeline — slower (demucs takes 30-180s
        // on CPU for one track) and isolated from playback. On completion, auto-
        // switches this deck to stem-mix playback (SwitchToStemMode below) via the
        // onStemsReady callback. See TrackAnalysisRun.KickOffStemAnalysis.
        _analysisRun.KickOffStemAnalysis(filePath);
    }

    /// <summary>Tear down whichever player(s) are currently in the deck mixer
    /// and aggressively release the previous track's heavy memory. For a 4-min
    /// stereo track at 48 kHz this is ~92 MB in the mixed-buffer mode, and up
    /// to ~370 MB in stem mode (4 × stems). Without explicit disposal here,
    /// the float[] backing each data provider survives until the next GC sweep,
    /// which on a long DJ set produces noticeable RAM creep across track changes.</summary>
    private void TearDownPlayers()
    {
        if (_player is not null)
        {
            _player.Stop();
            _deckMixer?.RemoveComponent(_player);
            _player.Dispose();
            _player = null;
        }
        // Dispose the data provider before nulling so the provider's own
        // cleanup (e.g. StemMixDataProvider null-outs the 4 stem buffers) runs.
        if (_currentDataProvider is not null)
        {
            try { _currentDataProvider.Dispose(); } catch { /* best-effort */ }
            _currentDataProvider = null;
        }
        _stemProvider = null;
        _scratchProvider = null;
        // Per-stem mute state lives inside StemMixDataProvider; reset by virtue
        // of dropping the reference. A fresh load builds a fresh provider with
        // all gains at 1.0.
        // A scratch mid-flight on the outgoing track has nothing left to drive —
        // drop the flag so a stale ScratchRate/EndScratch pair from the old
        // track can't act on the new one's provider. _scratching itself still
        // lives on Deck (the scratch cluster owns it); this is the one place
        // this class writes back into Deck rather than owning the state.
        _resetScratching();
    }

    /// <summary>Swap the SoundPlayer's data provider for a <see cref="StemMixDataProvider"/>
    /// that owns the 4 decoded stems and mixes them on demand. One player, one
    /// resampler, one position — same cost as single-track playback.
    /// Internal (not private) so Deck can wire it in as <c>TrackAnalysisRun</c>'s
    /// <c>onStemsReady</c> callback — see <see cref="TrackAnalysisRun"/>'s class
    /// doc for why this method itself stays on <c>TrackLoading</c> rather than
    /// moving into that class along with the rest of the analysis pipeline.</summary>
    internal void SwitchToStemMode(StemPaths stems)
    {
        if (_engine is null || _deckMixer is null) return;

        // Decode the 4 stems in parallel. Single-track Decode is ~1–3 s on a
        // 4-minute MP3, so doing them serially was a ~5× multiplier on stem load.
        // Task.Run lets the thread pool fan them out across cores; WhenAll
        // joins back when the slowest finishes.
        var dT = Task.Run(() => _decoder.Decode(stems.Drums));
        var vT = Task.Run(() => _decoder.Decode(stems.Vocals));
        var bT = Task.Run(() => _decoder.Decode(stems.Bass));
        var oT = Task.Run(() => _decoder.Decode(stems.Other));
        Task.WaitAll(dT, vT, bT, oT);
        var drums  = dT.Result;
        var vocals = vT.Result;
        var bass   = bT.Result;
        var other  = oT.Result;

        var posSeconds = _player?.Time ?? 0;
        var wasPlaying = IsPlaying;

        // Tear down the original single-buffer player AND dispose its data
        // provider so the previous track's full-mix float[] (held by the
        // RawDataProvider) can be reclaimed by GC immediately.
        if (_player is not null)
        {
            _player.Stop();
            _deckMixer.RemoveComponent(_player);
            _player.Dispose();
            _player = null;
        }
        if (_currentDataProvider is not null)
        {
            try { _currentDataProvider.Dispose(); } catch { /* best-effort */ }
            _currentDataProvider = null;
        }
        // The outgoing provider was _scratchProvider (Load's ScratchDataProvider) —
        // drop the reference alongside it so VarispeedProvider picks up the new
        // StemMixDataProvider below instead of a disposed one.
        _scratchProvider = null;

        var stemSamples = new StemSamples(drums, vocals, bass, other);
        var provider = new StemMixDataProvider(stemSamples, sampleRate: AudioFileDecoder.TargetSampleRate);
        _stemProvider = provider;
        _currentDataProvider = provider;
        _player = new SoundPlayer(_engine, _format, provider);
        _deckMixer.AddComponent(_player);
        // Speed is owned by the provider, not the SoundPlayer (see ApplyPlaybackSpeed).
        provider.SetSpeed(_playbackSpeed());
        _player.Volume = 1.0f; // unity — master gain applied downstream by the router (pre-fader cue tap)
        _reapplyGain();
        _player.Seek(TimeSpan.FromSeconds(Math.Max(0, posSeconds)));

        if (wasPlaying) _player.Play();
        Console.WriteLine("[Deck] switched to stem-mix playback (single player)");

        // Compute per-stem waveform peaks in the background. Each
        // WaveformPeaks.Compute is ~100-200 ms for a 4-min track; fan all four
        // out across cores so the slowest dictates total time. Once landed,
        // Analysis.Set fires StemPeaksReady → deck VM re-emits Peaks → waveform
        // rebakes against the current active-stem mask. See
        // TrackAnalysisRun.KickOffStemPeaksAnalysis.
        _analysisRun.KickOffStemPeaksAnalysis(stemSamples);
    }

    /// <inheritdoc/>
    public void Unload()
    {
        TearDownPlayers();
        _sampleCount = 0;
        _analysisRun.Analysis = new TrackAnalysis();
        _exitLoop();
        // Stem state lives inside StemMixDataProvider; TearDownPlayers drops the
        // reference, so the next track loads with all stems audible by default.
        AnalysisUpdated?.Invoke();
    }
}

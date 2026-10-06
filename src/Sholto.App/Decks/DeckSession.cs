using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Stems;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>See <see cref="IDeckSession"/>. The app logic and state that used to live in the deck view
/// model, with the view-model-only parts (brushes, ring colour, display strings, property
/// notifications) left behind. Threading: everything runs on the app thread; analysis events arrive on
/// worker threads and are posted onto it through <see cref="IAppThread"/>.</summary>
public sealed class DeckSession : IDeckSession
{
    private readonly IDeckPorts _ports;
    private readonly IDeckGain _gain;
    private readonly IDeckStemMix _stemMix;
    private readonly IDeckTuneEditor _tuneEditor;
    private readonly IDeckTempoControl _tempoControl;
    private readonly IDeckPlayPhase _playPhase;
    private readonly ISectionLayoutFactory _sectionLayouts;
    private readonly IAppThread _appThread;
    private readonly IEventPublisher _publisher;

    private SectionLayout _layout;

    private Track? _loadedTrack;
    private DeckLoadState _loadState = DeckLoadState.Idle;
    // The TrackAnalysis instance we are subscribed to, so we can unsubscribe when Loading.Analysis is
    // replaced (each BeginLoad creates a fresh TrackAnalysis on the player).
    private TrackAnalysis? _subscribedAnalysis;

    private bool _isScrubbing;
    private bool _isScratching;
    private double _magneticGlowSec = -1;

    public DeckSession(int index, IDeckPorts ports, IDeckGain gain, IDeckStemMix stemMix, IDeckTuneEditor tuneEditor, IDeckTempoControl tempoControl, IDeckPlayPhase playPhase, ISectionLayoutFactory sectionLayouts,
        IAppThread appThread, IEventPublisher publisher)
    {
        Index = index;
        _ports = ports;
        _gain = gain;
        _gain.Changed += Raise;
        _stemMix = stemMix;
        _stemMix.Changed += Raise;
        _tuneEditor = tuneEditor;
        _tuneEditor.Changed += Raise;
        _tempoControl = tempoControl;
        _tempoControl.Changed += Raise;
        _tempoControl.BpmMultiplierChosen += OnBpmMultiplierChosen;
        _playPhase = playPhase;
        _playPhase.Changed += Raise;
        _sectionLayouts = sectionLayouts;
        _layout = sectionLayouts.Empty();
        _appThread = appThread;
        _publisher = publisher;

        RebindAnalysisSubscription();
        Looping.LoopChanged += OnLoopChanged;
        Beatgrid.GridNudgedChanged += OnGridNudgedChanged;
        Mixer.CueChanged += OnCueChanged;

        // Publish the full starting picture so a subscriber that joins later is replayed it.
        _playPhase.PublishCurrent();
        _stemMix.PublishCurrent();
        PublishEcho();
    }

    public int Index { get; }

    public event Action<DeckChange>? Changed;
    public event Action<DeckLoadState>? LoadStateChanged;
    public event Action<string, double>? BpmMultiplierChosen;

    private void Raise(DeckChange change) => Changed?.Invoke(change);

    private void OnBpmMultiplierChosen(double multiplier)
    {
        if (LoadedTrack is not null)
            BpmMultiplierChosen?.Invoke(LoadedTrack.FilePath, multiplier);
    }

    // ---- Ports: each is the matching role of the injected IDeckPorts ---------------------------
    public ITransportControl Transport => _ports.Transport;
    public IDeckLooping Looping => _ports.Looping;
    public IDeckBeatgrid Beatgrid => _ports.Beatgrid;
    public IDeckTempo Tempo => _ports.Tempo;
    public IDeckMixer Mixer => _ports.Mixer;
    public IDeckEffects Effects => _ports.Effects;
    public IStemControl Stems => _ports.Stems;
    public ITrackLoading Loading => _ports.Loading;
    public IDeckScratch Scratch => _ports.Scratch;
    public IDeckPlayhead Playhead => _ports.Playhead;
    public IDeckReset Reset => _ports.Reset;
    public IEngineDeck Engine => _ports.Engine;

    // ---- Port events, marshalled onto the app thread -------------------------------------------
    private void OnLoopChanged(LoopRegion? _) => _appThread.Post(RaiseLoop);
    private void OnGridNudgedChanged(bool _) => _appThread.Post(RaiseGridNudged);
    private void OnCueChanged(bool _) => _appThread.Post(RaiseCue);

    private void RaiseLoop() => Raise(DeckChange.Loop);
    private void RaiseGridNudged() => Raise(DeckChange.GridNudged);
    private void RaiseCue() => Raise(DeckChange.Cue);

    /// <summary>(Re)subscribe to per-type events on <c>Loading.Analysis</c>. Called at construction and
    /// again after each load, because the player swaps in a fresh <see cref="TrackAnalysis"/> instance per
    /// track. The previously-subscribed instance is detached, otherwise old completed analyses would still
    /// notify against the wrong deck state and handlers would leak.</summary>
    private void RebindAnalysisSubscription()
    {
        if (_subscribedAnalysis is not null)
        {
            _subscribedAnalysis.BasicReady -= OnBasicReady;
            _subscribedAnalysis.KeyReady -= OnKeyReady;
            _subscribedAnalysis.StemsReady -= OnStemsReady;
            _subscribedAnalysis.VocalRegionsReady -= OnVocalRegionsReady;
        }
        _subscribedAnalysis = Loading.Analysis;
        _subscribedAnalysis.BasicReady += OnBasicReady;
        _subscribedAnalysis.KeyReady += OnKeyReady;
        _subscribedAnalysis.StemsReady += OnStemsReady;
        _subscribedAnalysis.VocalRegionsReady += OnVocalRegionsReady;
    }

    // Each per-type handler re-raises only what DEPENDS on that analysis type.
    private void OnBasicReady(BasicAnalysis _) => _appThread.Post(ApplyBasicReady);
    private void OnKeyReady(KeyAnalysis _) => _appThread.Post(RaiseKeyReady);
    private void OnStemsReady(StemPaths _) => _appThread.Post(RaiseStemsReady);
    private void OnVocalRegionsReady(IReadOnlyList<VocalRegion> _) => _appThread.Post(RaiseVocalRegionsReady);

    private void ApplyBasicReady()
    {
        // Recompute the phrase grid and sections from the peaks and the CURRENT grid. BasicReady also fires
        // for a grid nudge or BPM edit, so sections follow the grid; none are persisted.
        var basic = Analysis.Basic;
        if (basic is not null)
        {
            _layout = _sectionLayouts.Create(basic);
            Raise(DeckChange.Sections);
        }
        Raise(DeckChange.BasicReady);
    }

    private void RaiseKeyReady() => Raise(DeckChange.KeyReady);
    private void RaiseStemsReady() => Raise(DeckChange.StemsReady);
    private void RaiseVocalRegionsReady() => Raise(DeckChange.VocalRegionsReady);

    // ---- Load ----------------------------------------------------------------------------------
    public Track? LoadedTrack
    {
        get => _loadedTrack;
        private set { _loadedTrack = value; Raise(DeckChange.LoadedTrack); }
    }

    /// <summary>Lifecycle state of this deck's track. <see cref="LoadStateChanged"/> carries the typed
    /// transition for consumers that prefer one specific signal.</summary>
    public DeckLoadState LoadState
    {
        get => _loadState;
        private set
        {
            if (_loadState == value) return;
            _loadState = value;
            Raise(DeckChange.LoadState);
            LoadStateChanged?.Invoke(value);
        }
    }

    public bool IsLoaded => Loading.IsLoaded;

    public TrackAnalysis Analysis => Loading.Analysis;

    /// <summary>True once the track has completed basic analysis (BPM + beat grid). Magnet-lock and other
    /// analysis-derived features gate on it.</summary>
    public bool HasAnalysis => Analysis.Basic is not null;

    /// <summary>True once Demucs stems have landed for this track.</summary>
    public bool HasStems => Analysis.Get<StemPaths>() is not null;

    public Key? LoadedKey => Analysis.Get<KeyAnalysis>()?.Key;

    /// <summary>True when the deck is allowed to start playback. Gated on basic analysis being available:
    /// play is denied until the beat grid / BPM are known, because every downstream feature (magnet, sync,
    /// beat-jump, EffectiveBpm display) assumes that data exists.</summary>
    public bool CanPlay => LoadState == DeckLoadState.Loaded && HasAnalysis;

    /// <summary>Mark the in-progress load as failed (decode error etc.).</summary>
    public void LoadFailed() => LoadState = DeckLoadState.Failed;

    /// <summary>Update the deck for the incoming track immediately, before audio samples have been
    /// decoded. Clears stale analysis-derived state so the deck doesn't show the previous track's data under
    /// the new title for the 1-3 s decode wait. Audio for the previous track keeps playing until
    /// <see cref="LoadTrack"/> lands.</summary>
    public void BeginLoad(Track track, double bpmMultiplier = 1.0)
    {
        LoadState = DeckLoadState.Loading;
        _tempoControl.ClearMagnet();  // fresh track, no magnet activity yet
        // Reset every per-deck control so a new track starts with a clean signal path: no carryover from
        // what the previous track was doing (EQ kills, filter sweeps, attenuated stems, active loop, muted
        // pads). The audio side is wiped in Reset.ResetControls; the toggles + level state below are reset
        // in parallel so the published state matches.
        Reset.ResetControls();
        _stemMix.ResetForNewTrack();
        // ResetControls also switches the echo off on the audio side; say so.
        PublishEcho();
        Loading.BeginLoad(track.FilePath);
        // Loading.Analysis was just replaced with a fresh instance; hook onto it so the new track's
        // analyses reach this session, not the previous track's stale subscription.
        RebindAnalysisSubscription();
        LoadedTrack = track;
        _tempoControl.RestoreMultiplier(bpmMultiplier);
        IsPlaying = false;
        PlayPosition = 0;
        SetMarkers([]);                 // clear the old track's markers
        _layout = _sectionLayouts.Empty();
        Raise(DeckChange.Sections);
        Raise(DeckChange.AnalysisReset);
    }

    /// <summary>Streaming load: hands the file path to the audio engine so audio starts in ~100 ms, no
    /// upfront MP3 decode. Analysis (BPM/key) still happens in the background and unlocks features as each
    /// event lands.</summary>
    public void LoadStreaming(Track track, string filePath, double bpmMultiplier = 1.0)
    {
        Loading.LoadStreaming(filePath);
        RebindAnalysisSubscription();
        LoadedTrack = track;
        LoadState = DeckLoadState.Loaded;
        _tempoControl.RestoreMultiplier(bpmMultiplier);
        IsPlaying = false;
        PlayPosition = 0;
        Raise(DeckChange.IsLoaded);
        Raise(DeckChange.AnalysisReset);
    }

    public void LoadTrack(Track track, string filePath, float[] samples, double bpmMultiplier = 1.0)
    {
        Loading.Load(filePath, samples, sampleRate: AudioFileDecoder.TargetSampleRate);
        // Loading.Load replaces TrackAnalysis again (in case the caller skipped BeginLoad). Resubscribe so
        // per-type events from the new instance reach the session.
        RebindAnalysisSubscription();
        LoadedTrack = track;
        LoadState = DeckLoadState.Loaded;
        // Apply any persisted half / double override for this track. RestoreMultiplier does not raise
        // BpmMultiplierChosen: this came from disk, not the user.
        _tempoControl.RestoreMultiplier(bpmMultiplier);
        IsPlaying = false;
        PlayPosition = 0;
        Raise(DeckChange.IsLoaded);
        Raise(DeckChange.AnalysisReset);
    }

    public void Unload()
    {
        Loading.Unload();
        LoadedTrack = null;
        LoadState = DeckLoadState.Idle;
        _tempoControl.ClearMagnet();
        IsPlaying = false;
        PlayPosition = 0;
        Raise(DeckChange.IsLoaded);
        Raise(DeckChange.AnalysisReset);
    }

    // ---- Transport -----------------------------------------------------------------------------
    public bool IsPlaying
    {
        get => _playPhase.IsPlaying;
        private set => _playPhase.IsPlaying = value;
    }

    public PlayPhase PlayState => _playPhase.PlayState;

    public bool EndFlashOn => _playPhase.EndFlashOn;

    public double PlayPosition
    {
        get => _playPhase.PlayPosition;
        set => _playPhase.PlayPosition = value;
    }

    public double PlaybackSeconds => Playhead.PositionFrames / (double)AudioFileDecoder.TargetSampleRate;

    public void TogglePlay()
    {
        // Refuse to start playing until basic analysis (BPM + beat grid) is in. A silent ignore on the
        // first press is friendlier than a beep: the user usually presses again a moment later and it
        // works. Pause/resume is allowed freely once a track is actually playing.
        if (!Loading.IsPlaying && !CanPlay)
        {
            Console.WriteLine($"[Deck] play denied: analysis not ready (state={LoadState}, hasAnalysis={HasAnalysis})");
            return;
        }
        Transport.TogglePlay();
        IsPlaying = Loading.IsPlaying;
    }

    public void SyncPlayPosition()
    {
        // Transport state is owned by the audio Deck and can change without going through this session
        // (the play handler calls Transport.TogglePlay directly, the vinyl brake pauses from the scratch
        // engine, and a track simply ends). Follow the player here so PlayState / the BEAT SYNC LED / the
        // end-flash track reality. Skipped mid-scratch: the Deck force-starts a paused track while the
        // platter is held and restores the real state on release.
        if (!IsScratching) IsPlaying = Loading.IsPlaying;
        PlayPosition = Playhead.PlayPosition;
        _playPhase.UpdateFlash();
    }

    private void PublishEcho() => _publisher.Publish(new EchoChanged(Index, Effects.EchoActive));

    // ---- Beat positions ------------------------------------------------------------------------
    public double NearestDownbeatSec() => NearestIn(Analysis.Basic?.DownbeatTimes);

    private double NearestIn(double[]? times)
    {
        if (times is null || times.Length == 0) return -1;
        double pos = PlaybackSeconds;
        int idx = Array.BinarySearch(times, pos);
        if (idx >= 0) return times[idx];
        idx = ~idx;
        if (idx >= times.Length) return times[^1];
        if (idx == 0) return times[0];
        return Math.Abs(times[idx] - pos) < Math.Abs(times[idx - 1] - pos)
            ? times[idx] : times[idx - 1];
    }

    // ---- Markers, segments ---------------------------------------------------------------------
    public double[] MarkerSecs { get; private set; } = [];

    public void SetMarkers(IReadOnlyList<double> secs)
    {
        MarkerSecs = secs is double[] a ? a : secs.ToArray();
        Raise(DeckChange.Markers);
    }


    public IReadOnlyList<SongSection> Sections => _layout.Sections;

    public PhraseGrid PhraseGrid => _layout.Phrases;

    public Beatgrid SectionGrid => _layout.Grid;

    // ---- BPM multiplier ------------------------------------------------------------------------
    public double SourceBpm => _tempoControl.SourceBpm;

    public double BpmMultiplier => _tempoControl.BpmMultiplier;

    public void ToggleBpmOverride() => _tempoControl.ToggleBpmOverride();
    public void HalveBpm() => _tempoControl.HalveBpm();
    public void DoubleBpm() => _tempoControl.DoubleBpm();
    public void ResetBpmMultiplier() => _tempoControl.ResetBpmMultiplier();

    /// <summary>Reset both tempo and grid back to the analysed detection: clears the BPM override + the
    /// half/double multiplier and the phase offset.</summary>
    public void ResetToAnalysis()
    {
        Beatgrid.ResetGrid();          // clears BPM override + phase offset (deletes row)
        _tempoControl.ResetBpmMultiplier();          // half/double back to unity
    }

    public double EffectiveBpm => _tempoControl.EffectiveBpm;

    // ---- Tempo fader and range -----------------------------------------------------------------
    public bool IsTempoShifted => _tempoControl.IsTempoShifted;

    public bool WasMagnetAdjusted => _tempoControl.WasMagnetAdjusted;

    public bool MatchEffectiveBpm(double targetBpm) => _tempoControl.MatchEffectiveBpm(targetBpm);

    public void SetTempoPosition(double pos) => _tempoControl.SetTempoPosition(pos);

    public void SetTempoRange(double range) => _tempoControl.SetTempoRange(range);

    public void CycleTempoRange() => _tempoControl.CycleTempoRange();

    // ---- Grid edit, tune editor ----------------------------------------------------------------
    public bool GridEditActive => _tuneEditor.GridEditActive;

    public void ToggleGridEdit() => _tuneEditor.ToggleGridEdit();

    public void OnGridClick(double seconds) => _tuneEditor.OnGridClick(seconds);

    public bool EditOpen => _tuneEditor.EditOpen;

    public void ToggleEdit() => _tuneEditor.ToggleEdit();
    public void OpenEdit() => _tuneEditor.OpenEdit();
    public void CloseEdit() => _tuneEditor.CloseEdit();

    // ---- Stems ---------------------------------------------------------------------------------
    public bool VocalsActive
    {
        get => _stemMix.VocalsActive;
        set => _stemMix.VocalsActive = value;
    }

    public bool InstrumentalActive
    {
        get => _stemMix.InstrumentalActive;
        set => _stemMix.InstrumentalActive = value;
    }

    public bool DrumsActive
    {
        get => _stemMix.DrumsActive;
        set => _stemMix.DrumsActive = value;
    }

    public double DrumsLevel
    {
        get => _stemMix.DrumsLevel;
        set => _stemMix.DrumsLevel = value;
    }

    public double VocalsLevel
    {
        get => _stemMix.VocalsLevel;
        set => _stemMix.VocalsLevel = value;
    }

    public double InstrumentalLevel
    {
        get => _stemMix.InstrumentalLevel;
        set => _stemMix.InstrumentalLevel = value;
    }

    // ---- Performance flags ---------------------------------------------------------------------
    /// <summary>True while the user is actively turning the jog wheel on this deck. Drives the full-height
    /// guide line so the two decks' nearest downbeats can be eyeballed into alignment.</summary>
    public bool IsScrubbing
    {
        get => _isScrubbing;
        set
        {
            if (_isScrubbing == value) return;
            _isScrubbing = value;
            Raise(DeckChange.Scrubbing);
        }
    }

    /// <summary>True while a platter scratch is in flight on this deck (including the ~100 ms release decay
    /// back to rest); set by the scratch engine (<see cref="IsScrubbing"/> is the jog seek's). Gates
    /// magnetism/quantize so a beat-snap seek can't fire mid-scratch.</summary>
    public bool IsScratching
    {
        get => _isScratching;
        set
        {
            if (_isScratching == value) return;
            _isScratching = value;
            Raise(DeckChange.Scratching);
        }
    }

    /// <summary>Time of the beat that should glow green (magnetism active), or -1 = off.</summary>
    public double MagneticGlowSec
    {
        get => _magneticGlowSec;
        set
        {
            if (Math.Abs(_magneticGlowSec - value) < 0.001) return;
            _magneticGlowSec = value;
            Raise(DeckChange.MagneticGlow);
        }
    }

    // ---- Mixer, cue, echo ----------------------------------------------------------------------
    public double? ChannelGain
    {
        get => _gain.ChannelGain;
        set => _gain.ChannelGain = value;
    }

    public bool GainKnown => _gain.GainKnown;

    public double EffectiveGain => _gain.EffectiveGain;

    public bool IsMuted => _gain.IsMuted;

    public void SetCrossfadeGain(float gain) => _gain.SetCrossfadeGain(gain);

    /// <summary>Headphone cue (PFL) membership: the deck is summed into the ch3-4 headphone mix at full
    /// level, pre-fader. The Deck is the source of truth: the setter flows to it, it raises CueChanged and
    /// we re-raise.</summary>
    public bool CueActive
    {
        get => Mixer.CueActive;
        set => Mixer.CueActive = value;
    }

    /// <summary>The deck's beat-synced echo on/off state.</summary>
    public bool EchoActive
    {
        get => Effects.EchoActive;
        set
        {
            Effects.SetEcho(value);
            Raise(DeckChange.Echo);
            PublishEcho();
        }
    }
}

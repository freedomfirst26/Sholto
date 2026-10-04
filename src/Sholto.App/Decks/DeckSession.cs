using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
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
    /// <summary>Half of the end-of-track flash period: 400 ms lit, 400 ms dark (800 ms period, the old
    /// disc-ring rhythm).</summary>
    private const double FlashHalfPeriodMs = 400;

    private const double NearEndPosition = 0.9;

    // Rekordbox-style tempo-range stops: +-6 -> +-10 -> +-16 -> WIDE (+-100). The fader keeps its physical
    // position; only the span it maps to changes, so the current playback speed is preserved across a
    // range change as long as TempoPosition is re-applied (the TempoRange setter does this).
    private readonly double[] _tempoRangeStops = [0.06, 0.10, 0.16, 1.00];

    private readonly IDeckPorts _ports;
    private readonly ISongSegmentAnalyzer _songSegmentAnalyzer;
    private readonly IFrameClock _clock;
    private readonly IAppThread _appThread;
    private readonly IEventPublisher _publisher;

    private Track? _loadedTrack;
    private DeckLoadState _loadState = DeckLoadState.Idle;
    // The TrackAnalysis instance we are subscribed to, so we can unsubscribe when Loading.Analysis is
    // replaced (each BeginLoad creates a fresh TrackAnalysis on the player).
    private TrackAnalysis? _subscribedAnalysis;

    private bool _isPlaying;
    private double _playPosition;
    private PlayPhase _lastPlayState = PlayPhase.Stopped;
    private bool _flashOn;
    private DateTime _endingSince;

    private double _bpmMultiplier = 1.0;
    private bool _wasMagnetAdjusted;

    private bool _gridEditActive;
    private double? _gridAnchorA;
    private bool _editOpen;

    private bool _vocalsActive = true, _instrumentalActive = true, _drumsActive = true;
    private double _drumsLevel = 1.0, _vocalsLevel = 1.0, _instrumentalLevel = 1.0;

    private bool _isScrubbing;
    private bool _isScratching;
    private double _magneticGlowSec = -1;

    // Volume model: deck output = channel fader x crossfade gain.
    private float? _channelGain; // null = UNMEASURED: the FLX-4 fader hasn't been touched yet
    private float _crossfadeGain = 1.0f;

    public DeckSession(int index, IDeckPorts ports, ISongSegmentAnalyzer songSegmentAnalyzer,
        IFrameClock clock, IAppThread appThread, IEventPublisher publisher)
    {
        Index = index;
        _ports = ports;
        _songSegmentAnalyzer = songSegmentAnalyzer;
        _clock = clock;
        _appThread = appThread;
        _publisher = publisher;

        RebindAnalysisSubscription();
        Looping.LoopChanged += OnLoopChanged;
        Beatgrid.GridNudgedChanged += OnGridNudgedChanged;
        Mixer.CueChanged += OnCueChanged;
        // Channel gain starts UNMEASURED (null): the FLX-4 only reports a fader's position when moved, so
        // we don't know it yet. Applying it plays the deck at unity until the fader is first touched; the
        // UI shows no level until measured.
        ApplyVolume();

        // Publish the full starting picture so a subscriber that joins later is replayed it.
        PublishPlay();
        for (var stem = 0; stem < StemMuteChanged.StemsPerDeck; stem++) PublishStem(stem);
        PublishEcho();
    }

    public int Index { get; }

    public event Action<DeckChange>? Changed;
    public event Action<DeckLoadState>? LoadStateChanged;
    public event Action<string, double>? BpmMultiplierChosen;

    private void Raise(DeckChange change) => Changed?.Invoke(change);

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
        // Enrich with coarse structural sections (intro/build/drop/...) from the energy envelope +
        // beatgrid; drives the minimap colouring.
        var basic = Analysis.Basic;
        if (basic is not null)
        {
            Segments = _songSegmentAnalyzer.Analyze(
                basic.Peaks, basic.DownbeatTimes, AudioFileDecoder.TargetSampleRate);
            Console.WriteLine($"[Deck] song segments: {Segments.Count} " +
                $"(peaks={basic.Peaks.Min.Length}, downbeats={basic.DownbeatTimes.Length})");
            Raise(DeckChange.Segments);
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
        WasMagnetAdjusted = false;  // fresh track, no magnet activity yet
        // Reset every per-deck control so a new track starts with a clean signal path: no carryover from
        // what the previous track was doing (EQ kills, filter sweeps, attenuated stems, active loop, muted
        // pads). The audio side is wiped in Reset.ResetControls; the toggles + level state below are reset
        // in parallel so the published state matches.
        Reset.ResetControls();
        DrumsActive = true;
        VocalsActive = true;
        InstrumentalActive = true;
        DrumsLevel = 1.0;
        VocalsLevel = 1.0;
        InstrumentalLevel = 1.0;
        // ResetControls also switches the echo off on the audio side; say so.
        PublishEcho();
        Loading.BeginLoad();
        // Loading.Analysis was just replaced with a fresh instance; hook onto it so the new track's
        // analyses reach this session, not the previous track's stale subscription.
        RebindAnalysisSubscription();
        LoadedTrack = track;
        _bpmMultiplier = bpmMultiplier;
        Tempo.BpmMultiplier = bpmMultiplier;
        IsPlaying = false;
        PlayPosition = 0;
        Raise(DeckChange.BpmMultiplier);
        SetMarkers([]);                 // clear the old track's markers
        Segments = null;
        Raise(DeckChange.Segments);
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
        _bpmMultiplier = bpmMultiplier;
        Tempo.BpmMultiplier = bpmMultiplier;
        Raise(DeckChange.BpmMultiplier);
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
        // Apply any persisted half / double override for this track. Direct field set (not the property)
        // so BpmMultiplierChosen does not fire: this came from disk, not the user.
        _bpmMultiplier = bpmMultiplier;
        Tempo.BpmMultiplier = bpmMultiplier;
        Raise(DeckChange.BpmMultiplier);
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
        WasMagnetAdjusted = false;
        IsPlaying = false;
        PlayPosition = 0;
        Raise(DeckChange.IsLoaded);
        Raise(DeckChange.AnalysisReset);
    }

    // ---- Transport -----------------------------------------------------------------------------
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (_isPlaying == value) return;
            _isPlaying = value;
            Raise(DeckChange.IsPlaying);
            RefreshPlayState();
        }
    }

    public PlayPhase PlayState =>
        !_isPlaying ? PlayPhase.Stopped :
        IsNearEnd   ? PlayPhase.Ending :
                      PlayPhase.Playing;

    public bool IsNearEnd => _playPosition >= NearEndPosition;

    public bool EndFlashOn => _lastPlayState == PlayPhase.Ending && _flashOn;

    public double PlayPosition
    {
        get => _playPosition;
        set
        {
            _playPosition = value;
            Raise(DeckChange.PlayPosition);
            // Position can cross the near-end threshold without any play/pause event, so re-derive the
            // transport state here too (Playing -> Ending).
            RefreshPlayState();
        }
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
        UpdateFlash();
    }

    /// <summary>Recompute the play phase after a trigger (play/pause OR position crossing the near-end
    /// threshold). On a change: stamp the moment Ending began (the flash phase counts from it), and tell the
    /// view model and the bus.</summary>
    private void RefreshPlayState()
    {
        var state = PlayState;
        if (state == _lastPlayState) return;
        _lastPlayState = state;

        if (state == PlayPhase.Ending)
        {
            _endingSince = _clock.Now;
            _flashOn = true;
        }
        else
        {
            _flashOn = false;
        }

        Raise(DeckChange.PlayState);
        PublishPlay();
    }

    /// <summary>The single end-of-track flash: lit for the first <see cref="FlashHalfPeriodMs"/> after
    /// Ending began, then alternating. The phase is a pure function of the frame clock, so the controller
    /// light and the disc ring (both fed from here) cannot drift apart. Allocation-free.</summary>
    private void UpdateFlash()
    {
        if (_lastPlayState != PlayPhase.Ending) return;
        var elapsedMs = Math.Max(0, (_clock.Now - _endingSince).TotalMilliseconds);
        var on = ((long)(elapsedMs / FlashHalfPeriodMs) & 1L) == 0;
        if (on == _flashOn) return;
        _flashOn = on;
        Raise(DeckChange.EndFlash);
        PublishPlay();
    }

    private void PublishPlay() =>
        _publisher.Publish(new DeckPlayStateChanged(Index, _lastPlayState, EndFlashOn));

    private void PublishStem(int stem)
    {
        var active = stem switch
        {
            0 => _drumsActive,
            1 => _vocalsActive,
            _ => _instrumentalActive,
        };
        _publisher.Publish(new StemMuteChanged(Index, stem, !active));
    }

    private void PublishEcho() => _publisher.Publish(new EchoChanged(Index, Effects.EchoActive));

    // ---- Beat positions ------------------------------------------------------------------------
    public double NearestBeatSec() => NearestIn(Analysis.Basic?.BeatTimes);

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

    public IReadOnlyList<SongSegment>? Segments { get; private set; }

    // ---- BPM multiplier ------------------------------------------------------------------------
    public double SourceBpm => Analysis.Basic?.Bpm ?? 0;

    /// <summary>User-applied multiplier: drives BOTH the displayed BPM and the playback speed of the deck.
    /// Half slows the track to match a corrected reading, double the opposite.</summary>
    public double BpmMultiplier
    {
        get => _bpmMultiplier;
        private set
        {
            if (Math.Abs(_bpmMultiplier - value) < 0.0001) return;
            _bpmMultiplier = value;
            Tempo.BpmMultiplier = value;   // also halve/double the actual audio
            Raise(DeckChange.BpmMultiplier);
        }
    }

    private void SetMultiplierAndPersist(double newValue)
    {
        BpmMultiplier = newValue;
        if (LoadedTrack is not null)
            BpmMultiplierChosen?.Invoke(LoadedTrack.FilePath, newValue);
    }

    /// <summary>One-click flip-flop. If already overridden, returns to original (multiplier = 1). If at
    /// original, picks the most-likely correction: high BPM halves, low BPM doubles.</summary>
    public void ToggleBpmOverride()
    {
        if (Math.Abs(BpmMultiplier - 1.0) > 0.001)
        {
            SetMultiplierAndPersist(1.0);
            return;
        }
        // At unity. Pick a direction from the source BPM the analyser found. A threshold of 120 catches the
        // common cases: anything >= 120 was likely doubled by madmom and gets halved; anything < 120 gets
        // doubled.
        SetMultiplierAndPersist(SourceBpm >= 120 ? 0.5 : 2.0);
    }

    public void HalveBpm() => SetMultiplierAndPersist(BpmMultiplier * 0.5);
    public void DoubleBpm() => SetMultiplierAndPersist(BpmMultiplier * 2.0);
    public void ResetBpmMultiplier() => SetMultiplierAndPersist(1.0);

    // Tempo: up/down. Value stays decimal (never rounded). Coarse = whole BPM.
    public void BpmUp() => Beatgrid.AdjustBpm(+0.1);
    public void BpmDown() => Beatgrid.AdjustBpm(-0.1);
    public void BpmUpCoarse() => Beatgrid.AdjustBpm(+1.0);
    public void BpmDownCoarse() => Beatgrid.AdjustBpm(-1.0);

    // Grid: phase nudge.
    public void PhaseNudgeLeft() => Beatgrid.NudgeGridFine(-0.010);
    public void PhaseNudgeRight() => Beatgrid.NudgeGridFine(+0.010);

    /// <summary>Reset both tempo and grid back to the analysed detection: clears the BPM override + the
    /// half/double multiplier and the phase offset.</summary>
    public void ResetToAnalysis()
    {
        Beatgrid.ResetGrid();          // clears BPM override + phase offset (deletes row)
        ResetBpmMultiplier();          // half/double back to unity
    }

    /// <summary>Source BPM x user multiplier x current playback speed: what the user actually hears.</summary>
    public double EffectiveBpm => SourceBpm * _bpmMultiplier * Tempo.PlaybackSpeed;

    // ---- Tempo fader and range -----------------------------------------------------------------
    /// <summary>True only when the tempo FADER has moved off-centre. The half/double override doesn't
    /// trigger this: it is a correction to the analysed source, not a live performance shift.</summary>
    public bool IsTempoShifted => Math.Abs(Tempo.PlaybackSpeed - 1.0) > 0.002;

    /// <summary>True when the magnet snap retuned this deck's tempo to match its partner. Stays true until
    /// the user touches their tempo fader or a new track loads.</summary>
    public bool WasMagnetAdjusted
    {
        get => _wasMagnetAdjusted;
        private set
        {
            if (_wasMagnetAdjusted == value) return;
            _wasMagnetAdjusted = value;
            Raise(DeckChange.MagnetAdjusted);
        }
    }

    /// <summary>Adjust this deck's tempo fader so its <see cref="EffectiveBpm"/> matches
    /// <paramref name="targetBpm"/>. Used by magnet-snap: once two decks phase-align, locking their
    /// effective BPMs is what keeps them locked. If the required shift falls outside the tempo range,
    /// clamps to the edge of the fader range and gets as close as possible. Returns false on inputs that
    /// don't make sense (target &lt;= 0, no source BPM, no pitch range configured).</summary>
    public bool MatchEffectiveBpm(double targetBpm)
    {
        if (targetBpm <= 0) return false;
        double current = EffectiveBpm;
        if (current <= 0) return false;

        // Already at the target (within display precision): no shift needed and no chip-pop. The magnet
        // glyph already told the user they're locked; popping the OriginalBpm side chip here would be a
        // lie since the deck wasn't actually retuned.
        if (Math.Abs(targetBpm - current) < 0.01) return false;

        // Work in PlaybackSpeed-space to dodge any subtlety in how SourceBpm / BpmMultiplier compose.
        // EffectiveBpm scales linearly with PlaybackSpeed, so the ratio is what we need.
        double desiredPlaybackSpeed = Tempo.PlaybackSpeed * (targetBpm / current);

        double mult = _bpmMultiplier > 0 ? _bpmMultiplier : 1.0;
        double desiredFader = desiredPlaybackSpeed / mult;

        double range = Tempo.TempoRange;
        if (range <= 0) return false;

        // PlaybackSpeed fader = 1 + (-1 + 2*pos) * range  =>  pos = 0.5 + (fader-1)/(2*range).
        double pos = 0.5 + (desiredFader - 1.0) / (2.0 * range);
        ApplyTempoPosition(Math.Clamp(pos, 0.0, 1.0));
        // Flag this as a magnet-driven adjustment so the OriginalBpm chip pops out even though the actual
        // shift may be far below IsTempoShifted's 0.2 % threshold (a 176.5 -> 176.6 lock is only 0.06 %).
        WasMagnetAdjusted = true;
        return true;
    }

    /// <summary>Forward the FLX-4 tempo fader to the player. Position 0..1, 0.5 = unity. User-driven: also
    /// clears the magnet-adjusted flag, since touching the fader means "I'm taking control back."</summary>
    public void SetTempoPosition(double pos)
    {
        ApplyTempoPosition(pos);
        WasMagnetAdjusted = false;
    }

    /// <summary>Internal tempo-fader write that doesn't touch the magnet flag, so
    /// <see cref="MatchEffectiveBpm"/> can set <see cref="WasMagnetAdjusted"/> itself.</summary>
    private void ApplyTempoPosition(double pos)
    {
        Tempo.TempoPosition = pos;
        Raise(DeckChange.Tempo);
    }

    public void SetTempoRange(double range)
    {
        Tempo.TempoRange = range;
        Raise(DeckChange.TempoRange);
    }

    public void CycleTempoRange()
    {
        double current = Tempo.TempoRange;
        int idx = 0;
        for (int i = 0; i < _tempoRangeStops.Length; i++)
            if (Math.Abs(_tempoRangeStops[i] - current) < 1e-6) { idx = i; break; }
        double next = _tempoRangeStops[(idx + 1) % _tempoRangeStops.Length];
        SetTempoRange(next);
        Console.WriteLine($"[Deck] tempo range -> {(next >= 0.99 ? "WIDE" : $"+-{next * 100:F0}%")}");
    }

    // ---- Grid edit, tune editor ----------------------------------------------------------------
    // Two-point grid edit: 'G' toggles the mode; the first waveform click stores anchor A, the second
    // computes the exact BPM from the span and applies it, then exits the mode.
    public bool GridEditActive
    {
        get => _gridEditActive;
        private set
        {
            if (_gridEditActive == value) return;
            _gridEditActive = value;
            Raise(DeckChange.GridEdit);
        }
    }

    /// <summary>Toggle two-point grid-edit mode. Entering clears any half-set anchor; exiting discards a
    /// pending first click.</summary>
    public void ToggleGridEdit()
    {
        _gridAnchorA = null;
        GridEditActive = !GridEditActive;
        Console.WriteLine(GridEditActive
            ? "[Deck] grid edit ON - click an early kick, then a later kick"
            : "[Deck] grid edit OFF");
    }

    public void OnGridClick(double seconds)
    {
        if (!GridEditActive) return;
        if (_gridAnchorA is null)
        {
            _gridAnchorA = seconds;
            Console.WriteLine($"[Deck] grid anchor A = {seconds:F3}s - now click a later kick");
        }
        else
        {
            Beatgrid.SetGridFromTwoPoints(_gridAnchorA.Value, seconds);
            _gridAnchorA = null;
            GridEditActive = false;
        }
    }

    // Tune editor: click the BPM and one combined editor slides out over the disc. While it is open the
    // up/down keys adjust BPM and left/right nudge the grid.
    public bool EditOpen
    {
        get => _editOpen;
        private set
        {
            if (_editOpen == value) return;
            _editOpen = value;
            Raise(DeckChange.EditOpen);
        }
    }

    public void ToggleEdit() => EditOpen = !EditOpen;
    public void OpenEdit() => EditOpen = true;
    public void CloseEdit() => EditOpen = false;

    // ---- Stems ---------------------------------------------------------------------------------
    // Per-stem mute toggles. Default ON so a freshly-loaded track shows all three filled. Each change is
    // published for the controller's stem pads.
    public bool VocalsActive
    {
        get => _vocalsActive;
        set
        {
            if (_vocalsActive == value) return;
            _vocalsActive = value;
            Raise(DeckChange.VocalsActive);
            PublishStem(1);
        }
    }

    public bool InstrumentalActive
    {
        get => _instrumentalActive;
        set
        {
            if (_instrumentalActive == value) return;
            _instrumentalActive = value;
            Raise(DeckChange.InstrumentalActive);
            PublishStem(2);
        }
    }

    public bool DrumsActive
    {
        get => _drumsActive;
        set
        {
            if (_drumsActive == value) return;
            _drumsActive = value;
            Raise(DeckChange.DrumsActive);
            PublishStem(0);
        }
    }

    // Per-stem continuous level (0..1.5), driven by Shift + EQ knobs on the FLX-4. Default 1.0 (unity).
    // The setter pushes the level to the audio path.
    public double DrumsLevel
    {
        get => _drumsLevel;
        set
        {
            if (Math.Abs(_drumsLevel - value) < 1e-4) return;
            _drumsLevel = value;
            Stems.SetStemGroupLevel(0, value);
            Raise(DeckChange.DrumsLevel);
        }
    }

    public double VocalsLevel
    {
        get => _vocalsLevel;
        set
        {
            if (Math.Abs(_vocalsLevel - value) < 1e-4) return;
            _vocalsLevel = value;
            Stems.SetStemGroupLevel(1, value);
            Raise(DeckChange.VocalsLevel);
        }
    }

    public double InstrumentalLevel
    {
        get => _instrumentalLevel;
        set
        {
            if (Math.Abs(_instrumentalLevel - value) < 1e-4) return;
            _instrumentalLevel = value;
            Stems.SetStemGroupLevel(2, value);
            Raise(DeckChange.InstrumentalLevel);
        }
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
    /// <summary>Channel fader 0..1, or null until the fader is measured. The FLX-4 only reports a fader's
    /// position on movement, so at startup we don't know it; null is that unmeasured truth (distinct from
    /// 0). Unmeasured plays at unity (so there's sound after a restart); the first move adopts the real
    /// position.</summary>
    public double? ChannelGain
    {
        get => _channelGain;
        set
        {
            var v = value.HasValue ? (float)Math.Clamp(value.Value, 0, 1) : (float?)null;
            if (Nullable.Equals(v, _channelGain)) return;
            _channelGain = v;
            ApplyVolume();
            Raise(DeckChange.ChannelGain);
        }
    }

    /// <summary>False until the channel fader has been measured.</summary>
    public bool GainKnown => _channelGain.HasValue;

    /// <summary>Set when the crossfader moves.</summary>
    public void SetCrossfadeGain(float gain)
    {
        _crossfadeGain = Math.Clamp(gain, 0f, 1f);
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        // Unmeasured -> play at UNITY, not silent. The FLX-4 sends no fader position until moved, so gating
        // audio on measurement left every deck dead-silent after a restart until you jiggled the fader.
        // Default audible; the first physical move adopts the real position (soft-takeover has no prior
        // value to take over from). The UI still hides the gain line until GainKnown.
        Mixer.Volume = (_channelGain ?? 1f) * _crossfadeGain;
        Raise(DeckChange.Volume);
    }

    /// <summary>Combined channel x crossfade gain, 0..1 (0 when unmeasured).</summary>
    public double EffectiveGain => (_channelGain ?? 0f) * _crossfadeGain;

    /// <summary>True when the deck is measured AND effectively silent. An unmeasured deck is "unknown",
    /// not "muted".</summary>
    public bool IsMuted => GainKnown && EffectiveGain < 0.001;

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

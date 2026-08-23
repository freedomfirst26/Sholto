using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Audio;
using Sholto.Analysis;
using Sholto.App.Theming;
using Sholto.Music;

namespace Sholto.App.ViewModels;

/// <summary>Lifecycle states for a deck's currently-loading-or-loaded track.
/// Bindings/subscribers can react to transitions without inferring state from a
/// combination of LoadedTrack + Analysis nulls.</summary>
public enum DeckLoadState
{
    /// <summary>No track on this deck.</summary>
    Idle,
    /// <summary>Track metadata is showing, but audio samples are still being decoded.</summary>
    Loading,
    /// <summary>Samples decoded and wired up; deck is ready to play.</summary>
    Loaded,
    /// <summary>A previous load attempt failed (decode error, etc.).</summary>
    Failed,
}

/// <summary>Transport state of a deck.</summary>
public enum DeckPlayState
{
    /// <summary>Not playing (paused or never started).</summary>
    Stopped,
    /// <summary>Audio is playing.</summary>
    Playing,
    /// <summary>Playing, but past the near-end threshold — the deck is running out.
    /// Drives the synchronised end-of-track flash (red disc ring + controller LED).</summary>
    Ending,
}

public sealed class DeckViewModel : INotifyPropertyChanged
{
    private readonly Deck _player;
    private Track? _loadedTrack;
    private bool _isPlaying;
    private double _playPosition;
    private DeckLoadState _loadState = DeckLoadState.Idle;
    // Tracks which TrackAnalysis instance we're currently subscribed to so we
    // can unsubscribe from it when _player.Analysis gets replaced (each
    // BeginLoad creates a fresh TrackAnalysis on the player).
    private TrackAnalysis? _subscribedAnalysis;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeckViewModel(Deck player)
    {
        _player = player;
        RebindAnalysisSubscription();
        _player.LoopChanged += OnLoopChanged;
        _player.GridNudgedChanged += OnGridNudgedChanged;
        _player.CueChanged += OnCueChanged;
        // Channel gain starts UNMEASURED (null) — the FLX-4 only reports a fader's
        // position when moved, so we don't know it yet. Apply it so the deck is
        // silent (null → 0) until the fader is first touched, rather than blasting
        // at a software default. The UI likewise shows no level until measured.
        ApplyVolume();
    }

    private void OnLoopChanged(LoopRegion? _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Notify(nameof(IsLooping));
            Notify(nameof(LoopStartSec));
            Notify(nameof(LoopEndSec));
        });

    private void OnGridNudgedChanged(bool _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Notify(nameof(IsGridNudged));
        });

    private void OnCueChanged(bool _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(nameof(CueActive)));

    /// <summary>True after the user has tapped at least one nudge on this
    /// deck's grid (cleared on BeginLoad / ResetControls). The waveform
    /// uses this to recolour the loop band so the user can see at a glance
    /// "this loop is on my tuned grid, not madmom's default."</summary>
    public bool IsGridNudged => _player.IsGridNudged;

    // Two-point grid edit: 'G' toggles the mode; the first waveform click
    // stores anchor A, the second computes the exact BPM from the span and
    // applies it, then exits the mode.
    private bool _gridEditActive;
    private double? _gridAnchorA;
    public bool GridEditActive
    {
        get => _gridEditActive;
        private set { if (_gridEditActive == value) return; _gridEditActive = value; Notify(); }
    }

    /// <summary>Toggle two-point grid-edit mode. Entering clears any half-set
    /// anchor; exiting discards a pending first click.</summary>
    public void ToggleGridEdit()
    {
        _gridAnchorA = null;
        GridEditActive = !GridEditActive;
        Console.WriteLine(GridEditActive
            ? "[Deck] grid edit ON — click an early kick, then a later kick"
            : "[Deck] grid edit OFF");
    }

    /// <summary>Handle a waveform click while in grid-edit mode. First click =
    /// anchor A; second click = anchor B → compute + apply, exit mode.</summary>
    public void OnGridClick(double seconds)
    {
        if (!GridEditActive) return;
        if (_gridAnchorA is null)
        {
            _gridAnchorA = seconds;
            Console.WriteLine($"[Deck] grid anchor A = {seconds:F3}s — now click a later kick");
        }
        else
        {
            _player.SetGridFromTwoPoints(_gridAnchorA.Value, seconds);
            _gridAnchorA = null;
            GridEditActive = false;
        }
    }

    /// <summary>True while a beat-loop is engaged on this deck.</summary>
    public bool IsLooping => _player.ActiveLoop is not null;

    /// <summary>Loop-in point in seconds, or null when not looping. Bound to
    /// the waveform's loop band overlay.</summary>
    public double? LoopStartSec => _player.ActiveLoop is { } r
        ? r.StartSample / 2.0 / AudioFileDecoder.TargetSampleRate
        : null;

    /// <summary>Loop-out point in seconds, or null when not looping.</summary>
    public double? LoopEndSec => _player.ActiveLoop is { } r
        ? r.EndSample / 2.0 / AudioFileDecoder.TargetSampleRate
        : null;

    /// <summary>(Re)subscribe to per-type events on <c>_player.Analysis</c>.
    /// Called at construction time and again after each <see cref="BeginLoad"/>,
    /// because the player swaps in a fresh <see cref="TrackAnalysis"/> instance
    /// per track. We track the previously-subscribed instance so we can detach
    /// from it cleanly — otherwise old completed analyses would still notify
    /// against the wrong deck state and we'd leak handlers.</summary>
    private void RebindAnalysisSubscription()
    {
        if (_subscribedAnalysis is not null)
        {
            _subscribedAnalysis.BasicReady      -= OnBasicReady;
            _subscribedAnalysis.KeyReady        -= OnKeyReady;
            _subscribedAnalysis.StemsReady        -= OnStemsReady;
            _subscribedAnalysis.VocalRegionsReady -= OnVocalRegionsReady;
            _subscribedAnalysis.SongSegmentsReady -= OnSongSegmentsReady;
        }
        _subscribedAnalysis = _player.Analysis;
        _subscribedAnalysis.BasicReady        += OnBasicReady;
        _subscribedAnalysis.KeyReady          += OnKeyReady;
        _subscribedAnalysis.StemsReady        += OnStemsReady;
        _subscribedAnalysis.VocalRegionsReady += OnVocalRegionsReady;
        _subscribedAnalysis.SongSegmentsReady += OnSongSegmentsReady;
    }

    // Model-based sections (allin1) arrive after the instant heuristic; when they do,
    // they replace it (higher quality, real labels).
    private void OnSongSegmentsReady(SongSegments g) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (g.Segments.Count == 0) return;
            Segments = g;
            Console.WriteLine($"[Deck] AI song segments: {g.Segments.Count} (allin1)");
            Notify(nameof(Segments));
            Notify(nameof(HasSegments));
            Notify(nameof(SectionMapVisible));
        });

    // Each per-type handler only re-notifies the bindings that DEPEND on that
    // analysis type. Cheaper than the old "fire everything on AnalysisUpdated"
    // pattern and clearer about cause → effect when reading the code.
    private void OnBasicReady(BasicAnalysis _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Enrich with coarse structural sections (intro/build/drop/…) from the
            // energy envelope + beatgrid — drives the minimap colouring.
            var basic = Analysis.Basic;
            if (basic is not null)
            {
                Segments = SongSegmentAnalyzer.Analyze(
                    basic.Peaks, basic.DownbeatTimes, AudioFileDecoder.TargetSampleRate);
                Console.WriteLine($"[Deck] song segments: {Segments.Segments.Count} " +
                    $"(peaks={basic.Peaks.Min.Length}, downbeats={basic.DownbeatTimes.Length})");
                Notify(nameof(Segments));
                Notify(nameof(HasSegments));
                Notify(nameof(SectionMapVisible));
            }
            Notify(nameof(Analysis));
            Notify(nameof(HasAnalysis));
            Notify(nameof(HasBpm));
            Notify(nameof(SourceBpm));
            Notify(nameof(BpmDisplay));
            Notify(nameof(BpmDisplayShort));
            Notify(nameof(EffectiveBpm));
            Notify(nameof(Peaks));
            Notify(nameof(GridPeaks));
            Notify(nameof(BeatTimes));
            Notify(nameof(DownbeatTimes));
            Notify(nameof(CanPlay));   // unlocked once basic analysis lands
        });

    private void OnKeyReady(KeyAnalysis _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Notify(nameof(Camelot));
            Notify(nameof(KeyName));
            Notify(nameof(HasKey));
            Notify(nameof(KeyBrush));
        });

    private void OnStemsReady(StemPaths _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(nameof(HasStems)));

    private void OnVocalRegionsReady(VocalRegions _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(nameof(VocalRegions)));

    public Deck Player => _player;

    public Track? LoadedTrack
    {
        get => _loadedTrack;
        private set { _loadedTrack = value; Notify(); }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (_isPlaying == value) return;
            _isPlaying = value;
            Notify();
            RefreshPlayState();
        }
    }

    /// <summary>Transport state: Stopped when not playing, Ending in the last stretch
    /// (past <see cref="IsNearEnd"/>), else Playing.</summary>
    public DeckPlayState PlayState =>
        !_isPlaying ? DeckPlayState.Stopped :
        IsNearEnd   ? DeckPlayState.Ending :
                      DeckPlayState.Playing;

    /// <summary>Raised when this deck's transport state changes. The orchestrator
    /// listens for the enum; the LED itself follows <see cref="DeckLightChanged"/>.</summary>
    public event Action<DeckPlayState>? PlayStateChanged;

    /// <summary>Resolved BEAT SYNC LED state (already accounts for the end flash):
    /// solid on while Playing, blinking while Ending, off while Stopped. Raised on
    /// every state change AND on every flash tick so the LED and the disc ring stay
    /// in lockstep (one clock drives both — see <see cref="_endFlash"/>).</summary>
    public event Action<bool>? DeckLightChanged;

    private DeckPlayState _lastPlayState = DeckPlayState.Stopped;
    private bool _flashOn;
    private Avalonia.Threading.DispatcherTimer? _endFlash;

    /// <summary>The single end-of-track flash clock. 400 ms toggles (800 ms period)
    /// matches the old disc-ring rhythm. Drives both the ring opacity and the LED.</summary>
    private void EnsureFlashTimer()
    {
        if (_endFlash is not null) return;
        _endFlash = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };
        _endFlash.Tick += (_, _) =>
        {
            _flashOn = !_flashOn;
            Notify(nameof(DiscRingOpacity));
            DeckLightChanged?.Invoke(ResolveLight());
        };
    }

    /// <summary>Recompute PlayState after a trigger (play/pause OR position crossing
    /// the near-end threshold); if it changed, notify, start/stop the flash clock,
    /// and refresh the LED + ring.</summary>
    private void RefreshPlayState()
    {
        var state = PlayState;
        if (state == _lastPlayState) return;
        _lastPlayState = state;

        if (state == DeckPlayState.Ending)
        {
            _flashOn = true;
            EnsureFlashTimer();
            _endFlash!.Start();
        }
        else
        {
            _endFlash?.Stop();
            _flashOn = false;
        }

        Notify(nameof(PlayState));
        Notify(nameof(DiscRingOpacity));
        PlayStateChanged?.Invoke(state);
        DeckLightChanged?.Invoke(ResolveLight());
    }

    private bool ResolveLight() => PlayState switch
    {
        DeckPlayState.Playing => true,
        DeckPlayState.Ending  => _flashOn,
        _                     => false,
    };

    /// <summary>Disc-ring opacity: solid (1.0) normally; flashes 1.0↔0.35 in sync
    /// with the LED while the track is Ending. Bound by the DiscRing in the view.</summary>
    public double DiscRingOpacity =>
        PlayState == DeckPlayState.Ending ? (_flashOn ? 1.0 : 0.35) : 1.0;

    // Cached so we don't allocate a fresh brush every 16 ms — that was Avalonia's
    // binding system seeing a "new" IBrush per frame and invalidating the whole disc.
    private readonly Avalonia.Media.SolidColorBrush _discRingBrush =
        new(Avalonia.Media.Color.FromRgb(0x34, 0xF0, 0x6F));
    private double _lastRingNotifyPos = -1;

    public double PlayPosition
    {
        get => _playPosition;
        set
        {
            _playPosition = value;
            Notify();
            Notify(nameof(DiscAngle));
            Notify(nameof(IsNearEnd));
            // Position can cross the near-end threshold without any play/pause event,
            // so re-derive the transport state here too (Playing → Ending).
            RefreshPlayState();

            // Recolour the ring in-place and notify only when the visible colour
            // actually changes (every ~1 % of track length = ~2 s of music). Avoids
            // ~120 brush invalidations/sec across both decks.
            RecomputeRingColor();
            if (Math.Abs(_playPosition - _lastRingNotifyPos) > 0.01)
            {
                _lastRingNotifyPos = _playPosition;
                Notify(nameof(DiscRingBrush));
            }
        }
    }

    /// <summary>True once the play position is past 90% — used to drive the end-of-track flash.</summary>
    public bool IsNearEnd => _playPosition >= 0.9;

    /// <summary>
    /// Outer ring colour: green (0%) → yellow (50%) → orange (75%) → red (100%).
    /// One cached brush whose Color is mutated in place — no allocation per frame.
    /// </summary>
    public Avalonia.Media.IBrush DiscRingBrush => _discRingBrush;

    private void RecomputeRingColor()
    {
        (byte r, byte g, byte b) green  = (0x34, 0xF0, 0x6F);
        (byte r, byte g, byte b) yellow = (0xFF, 0xD6, 0x3D);
        (byte r, byte g, byte b) orange = (0xFF, 0x8C, 0x2A);
        (byte r, byte g, byte b) red    = (0xFF, 0x3D, 0x4E);
        double p = Math.Clamp(_playPosition, 0, 1);

        static (byte r, byte g, byte b) Lerp((byte r, byte g, byte b) a, (byte r, byte g, byte b) b, double t) =>
            ((byte)(a.r + (b.r - a.r) * t),
             (byte)(a.g + (b.g - a.g) * t),
             (byte)(a.b + (b.b - a.b) * t));

        var c = p switch
        {
            < 0.5  => Lerp(green,  yellow, p / 0.5),
            < 0.75 => Lerp(yellow, orange, (p - 0.5) / 0.25),
            _      => Lerp(orange, red,    (p - 0.75) / 0.25),
        };
        _discRingBrush.Color = Avalonia.Media.Color.FromRgb(c.r, c.g, c.b);
    }

    /// <summary>
    /// Rotation angle (degrees) for the deck disc overlay. One revolution per bar
    /// (4 beats) — matches the feel of a vinyl turntable at the song's tempo.
    /// </summary>
    public double DiscAngle
    {
        get
        {
            var basic = Analysis.Basic;
            if (basic is null || basic.Bpm <= 0) return 0;
            double playSeconds = _player.PositionFrames / (double)AudioFileDecoder.TargetSampleRate;
            double secondsPerBar = 60.0 / basic.Bpm * 4;
            return (playSeconds / secondsPerBar) * 360.0;
        }
    }

    public TrackAnalysis Analysis => _player.Analysis;

    /// <summary>
    /// Waveform peaks for rendering: always the mixed frequency-band peaks
    /// (low / mid / high) from basic analysis. The silhouette is the full mix
    /// and does NOT change when stems are muted — stem state is shown by the
    /// DRMS / VOX / INST chips and the audio, not by repainting the track, so
    /// the arrangement stays recognizable at a glance (the way Serato /
    /// Rekordbox behave).
    /// </summary>
    public WaveformPeaks Peaks => Analysis.Basic?.Peaks ?? WaveformPeaks.Empty;

    /// <summary>Always the basic mixed peaks (or Empty until basic analysis lands).
    /// Used by <see cref="Controls.WaveformControl"/> as the time-mapping reference
    /// so the beatgrid keeps scrolling even when every stem is muted and the
    /// stem-coloured body has gone dark.</summary>
    public WaveformPeaks GridPeaks => Analysis.Basic?.Peaks ?? WaveformPeaks.Empty;

    /// <summary>Vocal-presence regions derived from the isolated vocal stem, or null
    /// until stems land. Drives the green "vocals present" rectangle overlay on the
    /// waveform. Not part of the silhouette (which stays the full mix, see
    /// <see cref="Peaks"/>) — it's a separate presence layer on top.</summary>
    public VocalRegions? VocalRegions => Analysis.Get<VocalRegions>();

    /// <summary>Marker positions on the loaded track, in seconds — rendered as flags
    /// on the waveform. Populated by MainViewModel from MarkerService when a track
    /// loads; cleared on a new load.</summary>
    public double[] MarkerSecs { get; private set; } = [];
    public void SetMarkers(IReadOnlyList<double> secs)
    {
        MarkerSecs = secs is double[] a ? a : secs.ToArray();
        Notify(nameof(MarkerSecs));
    }

    /// <summary>Coarse structural sections of the loaded track (intro/build/drop/…),
    /// or null until basic analysis lands. Drives the minimap colouring.</summary>
    public SongSegments? Segments { get; private set; }

    /// <summary>True once song-section analysis has produced sections — the songmap
    /// (minimap) is only shown when this is true.</summary>
    public bool HasSegments => Segments is { Segments.Count: > 0 };

    /// <summary>Feature-flag gate for the section map (from <c>FeatureOptions</c>).
    /// Set once at construction by <c>MainViewModel</c>.</summary>
    public bool SectionMapEnabled { get; init; }

    /// <summary>The section-map strip is shown only when the feature flag is on AND
    /// sections have been analysed. Bound by the minimap's <c>IsVisible</c>.</summary>
    public bool SectionMapVisible => SectionMapEnabled && HasSegments;

    // ---- Live EQ meter (shown in the disc when there's no album art) ----------
    // Coarse frequency content at the playhead, straight from the band peaks — no
    // FFT, no extra buffers. The control does the attack/decay + peak-hold trace.
    public float EqLow  => BandAt(p => p.Low);
    public float EqMid  => BandAt(p => p.Mid);
    public float EqHigh => BandAt(p => p.High);

    private float BandAt(Func<WaveformPeaks, float[]> pick)
    {
        var pk = Analysis.Basic?.Peaks;
        if (pk is null || pk.Min.Length == 0) return 0f;
        var arr = pick(pk);
        if (arr.Length != pk.Min.Length) return 0f;
        int col = Math.Clamp((int)(_playPosition * pk.Min.Length), 0, pk.Min.Length - 1);
        return arr[col];
    }

    /// <summary>Jump to an absolute fraction of the track (0..1) — minimap click/drag.</summary>
    public void SeekToFraction(double fraction)
    {
        if (!_player.IsLoaded) return;
        _player.SeekToFraction(fraction);
        SyncPlayPosition();
    }

    public double[] BeatTimes => Analysis.Basic?.BeatTimes ?? [];
    public double[] DownbeatTimes => Analysis.Basic?.DownbeatTimes ?? [];

    /// <summary>Source BPM (from analysis), unscaled.</summary>
    public double SourceBpm => Analysis.Basic?.Bpm ?? 0;

    private double _bpmMultiplier = 1.0;
    /// <summary>User-applied multiplier — drives BOTH the displayed BPM and the
    /// playback speed of the deck. ½ slows the track to match a corrected
    /// reading (e.g. a "sped-up" YouTube grab heard as 176 plays at 88 after
    /// a click). ×2 the opposite. Persisted per-track in SQLite.</summary>
    public double BpmMultiplier
    {
        get => _bpmMultiplier;
        private set
        {
            if (Math.Abs(_bpmMultiplier - value) < 0.0001) return;
            _bpmMultiplier = value;
            _player.BpmMultiplier = value;   // also halve/double the actual audio
            Notify();
            Notify(nameof(EffectiveBpm));
            Notify(nameof(BpmDisplay));
            Notify(nameof(BpmDisplayShort));
            Notify(nameof(OriginalBpmDisplay));
            Notify(nameof(OriginalBpmShort));
            Notify(nameof(PlaybackSpeed));
        }
    }

    /// <summary>Owner sets this so deck VMs can hand changes back to MainViewModel
    /// (which persists them to SQLite and updates the library row).</summary>
    public Action<string, double>? PersistBpmMultiplier { get; set; }

    private void SetMultiplierAndPersist(double newValue)
    {
        BpmMultiplier = newValue;
        if (LoadedTrack is not null)
            PersistBpmMultiplier?.Invoke(LoadedTrack.FilePath, newValue);
    }

    /// <summary>One-click flip-flop. If already overridden, returns to original
    /// (multiplier = 1). If at original, picks the most-likely correction:
    /// high BPM ⇒ halve; low BPM ⇒ double. Click again to flip back.</summary>
    public void ToggleBpmOverride()
    {
        if (Math.Abs(BpmMultiplier - 1.0) > 0.001)
        {
            SetMultiplierAndPersist(1.0);
            return;
        }
        // At unity. Pick a direction based on the source BPM the analyser found.
        // Threshold of 120 catches the common cases: anything ≥120 was likely
        // doubled by madmom and gets halved; anything <120 gets doubled.
        SetMultiplierAndPersist(SourceBpm >= 120 ? 0.5 : 2.0);
    }

    public void HalveBpm()           => SetMultiplierAndPersist(BpmMultiplier * 0.5);
    public void DoubleBpm()          => SetMultiplierAndPersist(BpmMultiplier * 2.0);
    public void ResetBpmMultiplier() => SetMultiplierAndPersist(1.0);

    // ---- Tune editor (swing-out from the BPM) ---------------------------------
    // Click the BPM → one combined editor slides out over the disc. While it's
    // open ("edit mode"): ↑/↓ adjust BPM, ←/→ nudge the grid — both on the
    // keyboard and via the on-screen buttons. It also arms those keyboard keys
    // (see MainWindow key handler) and brightens the waveform grid.
    private bool _editOpen;
    public bool EditOpen
    {
        get => _editOpen;
        private set
        {
            if (_editOpen == value) return;
            _editOpen = value;
            Notify(nameof(EditOpen));
            Notify(nameof(GridTuneActive));
        }
    }

    /// <summary>True while the tune editor is open: the only state in which the
    /// keyboard tempo/grid keys work and the waveform grid is emphasised.</summary>
    public bool GridTuneActive => _editOpen;

    public void ToggleEdit() => EditOpen = !EditOpen;
    public void OpenEdit()   => EditOpen = true;
    public void CloseEdit()  => EditOpen = false;

    // Tempo — ↑/↓. Value stays decimal (never rounded). Coarse = whole BPM.
    public void BpmUp()         => _player.AdjustBpm(+0.1);
    public void BpmDown()       => _player.AdjustBpm(-0.1);
    public void BpmUpCoarse()   => _player.AdjustBpm(+1.0);
    public void BpmDownCoarse() => _player.AdjustBpm(-1.0);

    // Grid — ←/→ phase nudge.
    public void PhaseNudgeLeft()  => _player.NudgeGridFine(-0.010);
    public void PhaseNudgeRight() => _player.NudgeGridFine(+0.010);

    /// <summary>Reset both tempo and grid back to the analysed detection:
    /// clears the BPM override + ½/×2 multiplier and the phase offset.</summary>
    public void ResetToAnalysis()
    {
        _player.ResetGrid();          // clears BPM override + phase offset (deletes row)
        ResetBpmMultiplier();          // ½/×2 back to unity
    }

    /// <summary>Source BPM × user multiplier × current playback speed — what the user actually hears.</summary>
    public double EffectiveBpm => SourceBpm * _bpmMultiplier * _player.PlaybackSpeed;

    /// <summary>Live playback speed multiplier (1.0 = unity). Bound to the waveform
    /// so its visual width compresses/stretches with the tempo fader and ½/×2 button.</summary>
    public double PlaybackSpeed => _player.PlaybackSpeed;

    public string BpmDisplay =>
        SourceBpm > 0 ? $"{EffectiveBpm:F1} BPM" : "";

    /// <summary>Just the number, no "BPM" suffix — used inside the disc.</summary>
    public string BpmDisplayShort =>
        SourceBpm > 0 ? $"{EffectiveBpm:F1}" : "";

    public bool HasBpm => SourceBpm > 0;

    /// <summary>Lifecycle state of this deck's track. Drives any UI that wants to
    /// show "loading…" or disable controls during decode. Fires PropertyChanged
    /// on every transition so XAML / code-behind can subscribe via standard
    /// INotifyPropertyChanged plumbing. <see cref="LoadStateChanged"/> is also
    /// raised for consumers that prefer a typed event.</summary>
    public DeckLoadState LoadState
    {
        get => _loadState;
        private set
        {
            if (_loadState == value) return;
            _loadState = value;
            Notify();
            Notify(nameof(CanPlay));
            Notify(nameof(InfoOpacity));
            LoadStateChanged?.Invoke(value);
        }
    }

    /// <summary>Opacity for the deck's Info component (disc, title, BPM, key, stem
    /// chips) — it reacts to <see cref="LoadState"/>: dimmed while the track is still
    /// loading, full once <see cref="DeckLoadState.Loaded"/>. The waveform component
    /// stays at full opacity throughout.</summary>
    public double InfoOpacity => LoadState == DeckLoadState.Loaded ? 1.0 : LoadingInfoOpacity;

    /// <summary>Info opacity while a track is loading/idle (0.3 = 70% transparent).</summary>
    private const double LoadingInfoOpacity = 0.3;

    /// <summary>Typed-event mirror of <see cref="LoadState"/> changes. Subscribe
    /// here when you'd rather listen for one specific signal than filter the
    /// generic PropertyChanged stream.</summary>
    public event Action<DeckLoadState>? LoadStateChanged;

    /// <summary>True once the track on this deck has completed basic analysis
    /// (BPM + beat grid). Magnet-lock and other analysis-derived features gate
    /// on this so they only engage when the data they need is actually present.</summary>
    public bool HasAnalysis => Analysis.Basic is not null;

    /// <summary>True once Demucs stems have landed for this track. Until then
    /// the stem mute toggles do nothing audibly, so the chip row hides — the
    /// deck progressively unlocks each feature as its analysis becomes available.</summary>
    public bool HasStems => Analysis.Get<Sholto.Analysis.StemPaths>() is not null;

    /// <summary>Camelot code for the loaded track (e.g. "8B"), or empty if key
    /// analysis hasn't completed yet.</summary>
    public string Camelot => Analysis.Get<Sholto.Analysis.KeyAnalysis>()?.Camelot ?? "";

    /// <summary>Musical key name (e.g. "Cm"), shown as a secondary label under the Camelot code.</summary>
    public string KeyName => Analysis.Get<Sholto.Analysis.KeyAnalysis>()?.KeyName ?? "";

    public bool HasKey => !string.IsNullOrEmpty(Camelot);

    /// <summary>Avalonia brush coloured by the Camelot key — used to tint the deck's
    /// key chip so the same colour shows here and in the library row. Pulls
    /// hue/sat/lightness from the active <see cref="ThemeContext.Current"/>'s
    /// CamelotPalette so theme switches retone live.</summary>
    public Avalonia.Media.IBrush KeyBrush
    {
        get
        {
            if (string.IsNullOrEmpty(Camelot)) return Avalonia.Media.Brushes.Transparent;
            var p = ThemeContext.Current.CamelotPalette;
            uint rgb = CamelotKeys.Rgb(Camelot, p.HueOffset, p.Saturation, p.MajorLightness, p.MinorLightness);
            return new Avalonia.Media.SolidColorBrush(unchecked((uint)0xFF000000 | rgb));
        }
    }

    /// <summary>Re-emit theme-derived bindings after a theme switch.</summary>
    public void RefreshThemeBindings() => Notify(nameof(KeyBrush));

    /// <summary>Adjust this deck's tempo fader so its <see cref="EffectiveBpm"/>
    /// matches <paramref name="targetBpm"/>. Used by magnet-snap: once two decks
    /// phase-align, locking their effective BPMs is what keeps them locked. If
    /// the required shift falls outside <see cref="Deck.PitchRange"/>,
    /// clamps to the edge of the fader range and gets as close as possible.
    /// Returns false on inputs that don't make sense (target ≤ 0, no source BPM,
    /// no pitch range configured).</summary>
    public bool MatchEffectiveBpm(double targetBpm)
    {
        if (targetBpm <= 0) return false;
        double current = EffectiveBpm;
        if (current <= 0) return false;

        // Already at the target (within display precision) — no shift needed
        // and no chip-pop. The magnet glyph already told the user they're
        // locked; popping the OriginalBpm side chip here would be a lie since
        // the deck wasn't actually retuned.
        if (Math.Abs(targetBpm - current) < 0.01) return false;

        // Work in PlaybackSpeed-space to dodge any subtlety in how SourceBpm /
        // BpmMultiplier compose. EffectiveBpm scales linearly with PlaybackSpeed,
        // so the ratio is what we need.
        double desiredPlaybackSpeed = _player.PlaybackSpeed * (targetBpm / current);

        double mult = _bpmMultiplier > 0 ? _bpmMultiplier : 1.0;
        double desiredFader = desiredPlaybackSpeed / mult;

        double range = _player.PitchRange;
        if (range <= 0) return false;

        // PlaybackSpeed fader = 1 + (-1 + 2*pos) * range  ⇒  pos = 0.5 + (fader-1)/(2*range).
        double pos = 0.5 + (desiredFader - 1.0) / (2.0 * range);
        ApplyTempoPosition(Math.Clamp(pos, 0.0, 1.0));
        // Flag this as a magnet-driven adjustment so the OriginalBpm chip pops
        // out even though the actual shift may be far below IsTempoShifted's
        // 0.2 % threshold (a 176.5 → 176.6 lock is only 0.06 %).
        WasMagnetAdjusted = true;
        return true;
    }

    /// <summary>Forward the FLX-4 tempo fader to the player. Position 0..1, 0.5 = unity.
    /// User-driven — also clears the magnet-adjusted flag, since touching the
    /// fader means "I'm taking control back."</summary>
    public void SetTempoPosition(double pos)
    {
        ApplyTempoPosition(pos);
        WasMagnetAdjusted = false;
    }

    /// <summary>Internal tempo-fader write that doesn't touch the magnet flag.
    /// Used by <see cref="MatchEffectiveBpm"/> so the magnet-driven shift can
    /// set <see cref="WasMagnetAdjusted"/> itself.</summary>
    private void ApplyTempoPosition(double pos)
    {
        _player.TempoPosition = pos;
        Notify(nameof(BpmDisplay));
        Notify(nameof(BpmDisplayShort));
        Notify(nameof(EffectiveBpm));
        Notify(nameof(IsTempoShifted));
        Notify(nameof(ShowOriginalBpm));
        Notify(nameof(OriginalBpmDisplay));
        Notify(nameof(OriginalBpmShort));
        Notify(nameof(PlaybackSpeed));
    }

    /// <summary>Forward a pitch-range change to the player and refresh UI.</summary>
    public void SetPitchRange(double range)
    {
        _player.PitchRange = range;
        Notify(nameof(BpmDisplay));
        Notify(nameof(BpmDisplayShort));
        Notify(nameof(EffectiveBpm));
        Notify(nameof(IsTempoShifted));
        Notify(nameof(PitchRangeDisplay));
        Notify(nameof(PlaybackSpeed));
    }

    // Rekordbox-style tempo-range stops: ±6 → ±10 → ±16 → WIDE (±100). The
    // fader keeps its physical position; only the span it maps to changes,
    // so the current playback speed is preserved across a range change as
    // long as TempoPosition is re-applied (PitchRange setter does this).
    private static readonly double[] PitchRangeStops = { 0.06, 0.10, 0.16, 1.00 };

    /// <summary>Cycle the tempo / pitch-fader range to the next Rekordbox
    /// stop (±6 → ±10 → ±16 → WIDE → ±6). Bound to Shift + BEAT SYNC.</summary>
    public void CyclePitchRange()
    {
        double current = _player.PitchRange;
        int idx = 0;
        for (int i = 0; i < PitchRangeStops.Length; i++)
            if (Math.Abs(PitchRangeStops[i] - current) < 1e-6) { idx = i; break; }
        double next = PitchRangeStops[(idx + 1) % PitchRangeStops.Length];
        SetPitchRange(next);
        Console.WriteLine($"[Deck] pitch range → {PitchRangeDisplay}");
    }

    /// <summary>True only when the tempo *fader* has moved off-centre. The
    /// half/double BPM override doesn't trigger this — the override is a
    /// correction to the analysed source, not a live performance shift.</summary>
    public bool IsTempoShifted => Math.Abs(_player.PlaybackSpeed - 1.0) > 0.002;

    private bool _wasMagnetAdjusted;
    /// <summary>True when the magnet snap retuned this deck's tempo to match
    /// its partner. Stays true until the user touches their tempo fader or a
    /// new track loads. Drives <see cref="ShowOriginalBpm"/> so the user gets
    /// visual confirmation even for sub-percent magnet shifts that wouldn't
    /// trip <see cref="IsTempoShifted"/>.</summary>
    public bool WasMagnetAdjusted
    {
        get => _wasMagnetAdjusted;
        private set
        {
            if (_wasMagnetAdjusted == value) return;
            _wasMagnetAdjusted = value;
            Notify();
            Notify(nameof(ShowOriginalBpm));
        }
    }

    /// <summary>Should the "original BPM" side chip be popped out? True when the
    /// fader is meaningfully shifted OR when magnet snap just adjusted us.
    /// XAML's bpm-chip-top/bottom styles bind to this.</summary>
    public bool ShowOriginalBpm => IsTempoShifted || WasMagnetAdjusted;

    /// <summary>"Original" = source × half/double override, but without the live
    /// tempo-fader shift. That's the BPM the user thinks of as "the track's BPM"
    /// once they've corrected any madmom octave error.</summary>
    public string OriginalBpmDisplay =>
        SourceBpm > 0 ? $"{(SourceBpm * _bpmMultiplier):F1} BPM" : "";

    public string OriginalBpmShort =>
        SourceBpm > 0 ? $"{(SourceBpm * _bpmMultiplier):F1}" : "";

    /// <summary>"±6%" / "±10%" / "±16%" / "WIDE" — the current pitch-range mode.</summary>
    public string PitchRangeDisplay =>
        _player.PitchRange >= 0.99 ? "WIDE" : $"±{_player.PitchRange * 100:F0}%";

    /// <summary>Update the deck's UI for the incoming track *immediately*, before
    /// audio samples have been decoded. Clears stale analysis-derived bindings
    /// (waveform, BPM, key, beat grid) so the deck doesn't show the previous
    /// track's data under the new title for the 1–3 s decode wait.
    /// Audio for the previous track keeps playing until <see cref="LoadTrack"/>
    /// lands — this is purely a visual responsiveness fix.</summary>
    /// <summary>Mark the in-progress load as failed (decode error etc.). Lets
    /// the UI reflect the failure without us having to invent error semantics
    /// inside <see cref="LoadTrack"/>.</summary>
    public void LoadFailed() => LoadState = DeckLoadState.Failed;

    public void BeginLoad(Track track, double bpmMultiplier = 1.0)
    {
        LoadState = DeckLoadState.Loading;
        WasMagnetAdjusted = false;  // fresh track, no magnet activity yet
        // Reset every per-deck control so a new track always starts with a
        // clean signal path — no carryover from what the previous track was
        // doing (EQ kills, filter sweeps, attenuated stems, active loop,
        // muted pads). Audio side is wiped in Player.ResetControls; VM-side
        // toggles + level state below must be reset in parallel so the UI
        // bindings reflect the same clean state.
        _player.ResetControls();
        DrumsActive = true;
        VocalsActive = true;
        InstrumentalActive = true;
        DrumsLevel = 1.0;
        VocalsLevel = 1.0;
        InstrumentalLevel = 1.0;
        _player.BeginLoad();
        // _player.Analysis was just replaced with a fresh instance; hook our
        // per-type event handlers onto it so the new track's analyses notify
        // the correct VM (not the previous track's stale subscription).
        RebindAnalysisSubscription();
        LoadedTrack = track;
        _bpmMultiplier = bpmMultiplier;
        _player.BpmMultiplier = bpmMultiplier;
        IsPlaying = false;
        PlayPosition = 0;
        Notify(nameof(BpmMultiplier));
        Notify(nameof(Analysis));
        SetMarkers([]);                 // clear the old track's markers
        Segments = null; Notify(nameof(Segments)); Notify(nameof(HasSegments)); Notify(nameof(SectionMapVisible));
        Notify(nameof(Peaks));
        Notify(nameof(VocalRegions));   // clear the old track's vocal overlay
        Notify(nameof(BeatTimes));
        Notify(nameof(DownbeatTimes));
        Notify(nameof(BpmDisplay));
        Notify(nameof(BpmDisplayShort));
        Notify(nameof(SourceBpm));
        Notify(nameof(HasBpm));
        Notify(nameof(HasAnalysis));
        Notify(nameof(Camelot));
        Notify(nameof(KeyName));
        Notify(nameof(HasKey));
        Notify(nameof(KeyBrush));
        Notify(nameof(EffectiveBpm));
    }

    /// <summary>Streaming load: hands the file path to the audio engine via
    /// <see cref="Deck.LoadStreaming"/> so audio starts in ~100 ms, no
    /// upfront MP3 decode. Analysis (BPM/key) still happens in the background
    /// and unlocks features progressively as each event lands.</summary>
    public void LoadStreaming(Track track, string filePath, double bpmMultiplier = 1.0)
    {
        _player.LoadStreaming(filePath);
        // _player.LoadStreaming replaced TrackAnalysis with a fresh instance;
        // hook our per-type event handlers onto it.
        RebindAnalysisSubscription();
        LoadedTrack = track;
        LoadState = DeckLoadState.Loaded;
        _bpmMultiplier = bpmMultiplier;
        _player.BpmMultiplier = bpmMultiplier;
        Notify(nameof(BpmMultiplier));
        Notify(nameof(EffectiveBpm));
        IsPlaying = false;
        PlayPosition = 0;
        Notify(nameof(IsLoaded));
        Notify(nameof(Analysis));
        Notify(nameof(Peaks));
        Notify(nameof(BeatTimes));
        Notify(nameof(DownbeatTimes));
        Notify(nameof(BpmDisplay));
    }

    public void LoadTrack(Track track, string filePath, float[] samples, double bpmMultiplier = 1.0)
    {
        _player.Load(filePath, samples, sampleRate: AudioFileDecoder.TargetSampleRate);
        // _player.Load replaces TrackAnalysis again (in case the caller skipped
        // BeginLoad). Resubscribe so per-type events from the new instance
        // reach the VM.
        RebindAnalysisSubscription();
        LoadedTrack = track;
        LoadState = DeckLoadState.Loaded;
        // Apply any persisted ½ / ×2 override for this track. Direct field set
        // (not the property) so we don't fire PersistBpmMultiplier — this came
        // from disk, not the user.
        _bpmMultiplier = bpmMultiplier;
        _player.BpmMultiplier = bpmMultiplier;
        Notify(nameof(BpmMultiplier));
        Notify(nameof(EffectiveBpm));
        IsPlaying = false;
        PlayPosition = 0;
        Notify(nameof(IsLoaded));
        Notify(nameof(Analysis));
        Notify(nameof(Peaks));
        Notify(nameof(BeatTimes));
        Notify(nameof(DownbeatTimes));
        Notify(nameof(BpmDisplay));
    }

    /// <summary>True when the deck is allowed to start playback. Gated on basic
    /// analysis being available — we deliberately deny play until the beat grid /
    /// BPM are known, because every downstream feature (magnet, sync, beat-jump,
    /// EffectiveBpm display) assumes that data exists. A small one-time wait per
    /// load is the price for keeping the rest of the app honest.</summary>
    public bool CanPlay => LoadState == DeckLoadState.Loaded && HasAnalysis;

    public void TogglePlay()
    {
        // Refuse to start playing until basic analysis (BPM + beat grid) is in.
        // Silent ignore on the *first* press is friendlier than a beep — the
        // user usually just presses again a moment later and it works. Allow
        // pause/resume freely once a track is actually playing.
        if (!_player.IsPlaying && !CanPlay)
        {
            Console.WriteLine($"[Deck] play denied: analysis not ready (state={LoadState}, hasAnalysis={HasAnalysis})");
            return;
        }
        _player.TogglePlay();
        IsPlaying = _player.IsPlaying;
    }

    public void Unload()
    {
        _player.Unload();
        LoadedTrack = null;
        LoadState = DeckLoadState.Idle;
        WasMagnetAdjusted = false;
        IsPlaying = false;
        PlayPosition = 0;
        Notify(nameof(Analysis));
        Notify(nameof(Peaks));
        Notify(nameof(BeatTimes));
        Notify(nameof(DownbeatTimes));
        Notify(nameof(BpmDisplay));
        Notify(nameof(BpmDisplayShort));
        Notify(nameof(HasBpm));
        Notify(nameof(IsLoaded));
    }

    public bool IsLoaded => _player.IsLoaded;

    /// <summary>Current playback time in seconds (drift-free, from the data provider).</summary>
    public double PlaybackSeconds => _player.PositionFrames / (double)AudioFileDecoder.TargetSampleRate;

    /// <summary>Time of the beat nearest the current playback position, or -1 if no beats.</summary>
    public double NearestBeatSec()      => NearestIn(Analysis.Basic?.BeatTimes);
    /// <summary>Time of the downbeat (bar-start) nearest the current playback position, or -1.</summary>
    public double NearestDownbeatSec() => NearestIn(Analysis.Basic?.DownbeatTimes);

    private double NearestIn(double[]? times)
    {
        if (times is null || times.Length == 0) return -1;
        double pos = PlaybackSeconds;
        int idx = Array.BinarySearch(times, pos);
        if (idx >= 0) return times[idx];
        idx = ~idx;
        if (idx >= times.Length) return times[^1];
        if (idx == 0)            return times[0];
        return Math.Abs(times[idx] - pos) < Math.Abs(times[idx - 1] - pos)
            ? times[idx] : times[idx - 1];
    }

    // Stem state — drives the per-stem mute toggles (audio) and the DRMS / VOX /
    // INST chips. The main waveform is deliberately NOT repainted on toggle: the
    // silhouette stays the full-mix frequency view so the track stays
    // recognizable. Default ON so a freshly-loaded track shows all three filled.
    private bool _vocalsActive = true, _instrumentalActive = true, _drumsActive = true;
    public bool VocalsActive
    {
        get => _vocalsActive;
        set { if (_vocalsActive == value) return; _vocalsActive = value; Notify(); }
    }
    public bool InstrumentalActive
    {
        get => _instrumentalActive;
        set { if (_instrumentalActive == value) return; _instrumentalActive = value; Notify(); }
    }
    public bool DrumsActive
    {
        get => _drumsActive;
        set { if (_drumsActive == value) return; _drumsActive = value; Notify(); }
    }

    // Per-stem continuous level (0..1.5), driven by Shift + EQ knobs on the
    // FLX-4. Default 1.0 (unity). Setter pushes the level to the audio path;
    // the main waveform is unaffected (frequency view, stable silhouette).
    private double _drumsLevel = 1.0, _vocalsLevel = 1.0, _instrumentalLevel = 1.0;
    public double DrumsLevel
    {
        get => _drumsLevel;
        set
        {
            if (Math.Abs(_drumsLevel - value) < 1e-4) return;
            _drumsLevel = value;
            Player.SetStemGroupLevel(0, value);
            Notify();
        }
    }
    public double VocalsLevel
    {
        get => _vocalsLevel;
        set
        {
            if (Math.Abs(_vocalsLevel - value) < 1e-4) return;
            _vocalsLevel = value;
            Player.SetStemGroupLevel(1, value);
            Notify();
        }
    }
    public double InstrumentalLevel
    {
        get => _instrumentalLevel;
        set
        {
            if (Math.Abs(_instrumentalLevel - value) < 1e-4) return;
            _instrumentalLevel = value;
            Player.SetStemGroupLevel(2, value);
            Notify();
        }
    }

    private bool _isScrubbing;
    /// <summary>True while the user is actively turning the jog wheel on this deck.
    /// Drives the full-height green guide line so the two decks' nearest downbeats
    /// can be eyeballed into alignment during a manual sync.</summary>
    public bool IsScrubbing
    {
        get => _isScrubbing;
        set { if (_isScrubbing == value) return; _isScrubbing = value; Notify(); }
    }

    private double _magneticGlowSec = -1;
    /// <summary>Time of the beat that should glow green (magnetism active), or -1 = off.
    /// Driven from <see cref="MainViewModel"/> since magnetism crosses both decks.</summary>
    public double MagneticGlowSec
    {
        get => _magneticGlowSec;
        set
        {
            if (Math.Abs(_magneticGlowSec - value) < 0.001) return;
            _magneticGlowSec = value;
            Notify();
        }
    }

    // Volume model: deck output = channel fader × crossfade gain.
    private float? _channelGain; // null = UNMEASURED — the FLX-4 fader hasn't been touched yet
    private float _crossfadeGain = 1.0f;

    /// <summary>Channel fader 0..1, or null until the fader is measured. The FLX-4
    /// only reports a fader's position on movement, so at startup we don't know it;
    /// null is that unmeasured truth (distinct from 0). Unmeasured → the deck plays
    /// at unity (so there's sound after a restart) but the UI shows no level until
    /// the first move (see <see cref="GainKnown"/> and <see cref="ApplyVolume"/>).</summary>
    public double? ChannelGain
    {
        get => _channelGain;
        set
        {
            var v = value.HasValue ? (float)Math.Clamp(value.Value, 0, 1) : (float?)null;
            if (Nullable.Equals(v, _channelGain)) return;
            _channelGain = v;
            ApplyVolume();
            Notify();
        }
    }

    /// <summary>False until the channel fader has been measured. The waveform hides
    /// the gain line and the mute tint until then — we don't render a value we
    /// haven't measured.</summary>
    public bool GainKnown => _channelGain.HasValue;

    /// <summary>Set by MainViewModel when the crossfader moves; not bound from UI.</summary>
    internal void SetCrossfadeGain(float gain)
    {
        _crossfadeGain = Math.Clamp(gain, 0f, 1f);
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        // Unmeasured → play at UNITY, not silent. The FLX-4 sends no fader position
        // until moved, so gating audio on measurement left every deck dead-silent
        // after a restart until you jiggled the fader. Default audible; the first
        // physical move adopts the real position (soft-takeover has no prior value
        // to take over from). The UI still hides the gain line until GainKnown.
        _player.Volume = (_channelGain ?? 1f) * _crossfadeGain;
        Notify(nameof(EffectiveGain));
        Notify(nameof(IsMuted));
        Notify(nameof(GainKnown));
    }

    /// <summary>Headphone cue (PFL) membership — the deck is summed into the ch3-4
    /// headphone mix at full level, pre-fader, so it can be pre-listened even with
    /// the channel fader down. Observed from the Deck (the source of truth): the
    /// setter flows to the Deck, which raises CueChanged, and we re-notify.</summary>
    public bool CueActive
    {
        get => _player.CueActive;
        set => _player.CueActive = value;
    }

    /// <summary>Combined channel × crossfade gain, 0..1 (0 when unmeasured). Used to
    /// draw the gain line on the waveform — but only when <see cref="GainKnown"/>.</summary>
    public double EffectiveGain => (_channelGain ?? 0f) * _crossfadeGain;

    /// <summary>True when the deck is measured AND effectively silent. Only counts as
    /// muted once the fader is known — an unmeasured deck is "unknown", not "muted".
    /// Drives the red mute tint over the deck area.</summary>
    public bool IsMuted => GainKnown && EffectiveGain < 0.001;

    public void SyncPlayPosition()
    {
        PlayPosition = _player.PlayPosition;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

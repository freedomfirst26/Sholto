using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The deck as the screen shows it: a projection of the App's deck events. It subscribes to the
/// events for its deck, caches what it displays, and turns each event into the property notifications the
/// bindings need. It keeps what is Avalonia-facing (brushes, the disc ring colour and opacity, the disc
/// angle, display strings). It decides nothing: every action on screen is a command sent through
/// <see cref="ICommandSender"/>, and the App answers with events.
/// <para>Events are published on the app thread, which is the UI thread, so handlers update directly. The
/// per-frame <see cref="DeckFrame"/> handler allocates nothing: it compares, stores and notifies through
/// cached <see cref="PropertyChangedEventArgs"/>.</para></summary>
public sealed class DeckViewModel :
    INotifyPropertyChanged,
    IEventHandler<DeckFrame>,
    IEventHandler<DeckContentChanged>,
    IEventHandler<DeckSectionsChanged>,
    IEventHandler<DeckTempoChanged>,
    IEventHandler<DeckLoopChanged>,
    IEventHandler<DeckEditChanged>,
    IEventHandler<DeckMarkersChanged>,
    IEventHandler<DeckMixChanged>,
    IEventHandler<DeckPlayStateChanged>,
    IEventHandler<StemMuteChanged>,
    IEventHandler<StemLevelChanged>,
    IEventHandler<EchoChanged>,
    IEventHandler<HeadphoneCueChanged>
{
    private readonly int _deck;
    // One PropertyChangedEventArgs per property name, so the per-frame notifications allocate nothing.
    private readonly Dictionary<string, PropertyChangedEventArgs> _args = [];

    // The disc bloom's smoothing state; the same instance for the life of the view model.
    private readonly IDiscBloom _bloom;
    private readonly IDeckTuneEditorCommands _editor;
    private readonly IDeckChipLevels _chips;
    private readonly IDeckRingColour _ring;
    private readonly IDeckTempoDisplay _tempo;
    private readonly IDeckAnalysisDisplay _display;
    private readonly IDeckSectionMap _sectionMap;

    // Property names announced together, in the order the bindings are told.
    private readonly string[] _contentNames =
    [
        nameof(Analysis), nameof(Peaks), nameof(GridPeaks), nameof(VocalRegions), nameof(BeatTimes),
        nameof(DownbeatTimes), nameof(HasAnalysis), nameof(HasStems), nameof(Camelot), nameof(HasKey), nameof(KeyBrush)
    ];
    private readonly string[] _sectionNames =
    [
        nameof(Sections), nameof(PhraseGrid), nameof(FirstDownbeatSec), nameof(BarPeriodSec), nameof(TotalBars),
        nameof(HasSections), nameof(SectionMapVisible)
    ];
    private readonly string[] _tempoNames =
    [
        nameof(SourceBpm), nameof(HasBpm), nameof(BpmMultiplier), nameof(EffectiveBpm), nameof(BpmDisplay),
        nameof(BpmDisplayShort), nameof(OriginalBpmDisplay), nameof(OriginalBpmShort), nameof(IsTempoShifted),
        nameof(WasMagnetAdjusted), nameof(ShowOriginalBpm), nameof(TempoRangeDisplay)
    ];
    private readonly string[] _mixNames = [nameof(ChannelGain), nameof(GainKnown), nameof(EffectiveGain), nameof(IsMuted)];
    private readonly string[] _stemActiveNames = [nameof(DrumsActive), nameof(VocalsActive), nameof(InstrumentalActive)];
    private readonly string[] _stemOpacityNames =
        [nameof(DrumsChipOpacity), nameof(VocalsChipOpacity), nameof(InstrumentalChipOpacity)];

    // ---- What the App last told us ------------------------------------------------------------
    private DeckTrack? _loadedTrack;
    private DeckLoadState _loadState = DeckLoadState.Idle;
    private bool _isLoaded;

    private PlayPhase _playState = PlayPhase.Stopped;
    private bool _endFlashOn;
    private double _playPosition;
    private double _playbackSeconds;
    private double _playbackSpeed = 1.0;
    private bool _isScrubbing;
    private bool _isScratching;
    private double _magneticGlowSec = -1;

    private bool _isLooping;
    private double _loopStartSec;
    private double _loopEndSec;

    private bool _editOpen;
    private bool _gridEditActive;
    private bool _isGridNudged;

    private double[] _markerSecs = [];

    private DeckMixChanged _mix;

    private bool _cueActive;
    private bool _echoActive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeckViewModel(
        int deck, IEventSubscriber subscriber, IDiscBloomFactory bloomFactory,
        IDeckTuneEditorCommands editor, IDeckChipLevels chips, IDeckRingColour ring,
        IDeckTempoDisplay tempo, IDeckAnalysisDisplay display, IDeckSectionMap sectionMap)
    {
        _deck = deck;
        _bloom = bloomFactory.Create();
        _editor = editor;
        _chips = chips;
        _ring = ring;
        _tempo = tempo;
        _display = display;
        _sectionMap = sectionMap;

        // Each subscribe replays the current state, so a view model built late shows the real picture.
        // Every field above is assigned first: the replay calls the handlers synchronously.
        subscriber.Subscribe<DeckContentChanged>(this);
        subscriber.Subscribe<DeckSectionsChanged>(this);
        subscriber.Subscribe<DeckTempoChanged>(this);
        subscriber.Subscribe<DeckLoopChanged>(this);
        subscriber.Subscribe<DeckEditChanged>(this);
        subscriber.Subscribe<DeckMarkersChanged>(this);
        subscriber.Subscribe<DeckMixChanged>(this);
        subscriber.Subscribe<DeckPlayStateChanged>(this);
        subscriber.Subscribe<StemMuteChanged>(this);
        subscriber.Subscribe<StemLevelChanged>(this);
        subscriber.Subscribe<EchoChanged>(this);
        subscriber.Subscribe<HeadphoneCueChanged>(this);
        subscriber.Subscribe<DeckFrame>(this);
    }

    // ---- Actions: each one is a command (sent by the tune-editor commands) ----------------------
    public void ToggleEditor() => _editor.ToggleEditor();
    public void CloseEditor() => _editor.CloseEditor();
    public void ChangeBpm(BpmMultiplierOp op) => _editor.ChangeBpm(op);
    public void AdjustBpm(double delta) => _editor.AdjustBpm(delta);
    public void NudgeGridFine(double seconds) => _editor.NudgeGridFine(seconds);
    public void NudgeGrid(int beats) => _editor.NudgeGrid(beats);
    public void ResetToAnalysis() => _editor.ResetToAnalysis();
    public void ClickGrid(double seconds) => _editor.ClickGrid(seconds);

    // ---- Events: the App tells us -------------------------------------------------------------
    /// <summary>The hot path (once per frame per deck): compare, store, notify only what moved.</summary>
    void IEventHandler<DeckFrame>.Handle(in DeckFrame e)
    {
        if (e.Deck != _deck) return;

        if (e.PlaybackSpeed != _playbackSpeed)
        {
            _playbackSpeed = e.PlaybackSpeed;
            Notify(nameof(PlaybackSpeed));
        }
        if (e.IsScrubbing != _isScrubbing)
        {
            _isScrubbing = e.IsScrubbing;
            Notify(nameof(IsScrubbing));
        }
        if (e.IsScratching != _isScratching)
        {
            _isScratching = e.IsScratching;
            Notify(nameof(IsScratching));
        }
        if (e.MagneticGlowSec != _magneticGlowSec)
        {
            _magneticGlowSec = e.MagneticGlowSec;
            Notify(nameof(MagneticGlowSec));
        }
        if (e.PlayPosition != _playPosition || e.PlaybackSeconds != _playbackSeconds)
        {
            _playPosition = e.PlayPosition;
            _playbackSeconds = e.PlaybackSeconds;
            OnPlayPositionChanged();
        }
        // Every frame, not only when the playhead moved: a paused deck's glows settle onto the playhead's
        // value, and an emptied deck's glows fade out. Allocates nothing (see DiscBloom).
        _bloom.Advance(EqLow, EqMid, EqHigh);
    }

    private void OnPlayPositionChanged()
    {
        Notify(nameof(PlayPosition));
        Notify(nameof(DiscAngle));
        if (_ring.MoveTo(_playPosition)) Notify(nameof(DiscRingBrush));
    }

    void IEventHandler<DeckContentChanged>.Handle(in DeckContentChanged e)
    {
        if (e.Deck != _deck) return;
        var loadStateChanged = e.LoadState != _loadState;
        var loadedChanged = e.IsLoaded != _isLoaded;
        _loadedTrack = e.Track;
        _loadState = e.LoadState;
        _isLoaded = e.IsLoaded;
        _display.Set(e.Analysis);

        // The content event fires for the load itself and for each analysis step that lands, so everything
        // derived from the track or its analysis is re-announced. Cheap: it happens a few times per load.
        Notify(nameof(LoadedTrack));
        if (loadStateChanged)
        {
            Notify(nameof(LoadState));
            Notify(nameof(InfoOpacity));
        }
        if (loadedChanged) Notify(nameof(IsLoaded));
        Notify(nameof(CanPlay));
        foreach (var name in _contentNames) Notify(name);
    }

    void IEventHandler<DeckSectionsChanged>.Handle(in DeckSectionsChanged e)
    {
        if (e.Deck != _deck) return;
        _sectionMap.Apply(e);
        foreach (var name in _sectionNames) Notify(name);
    }

    void IEventHandler<DeckTempoChanged>.Handle(in DeckTempoChanged e)
    {
        if (e.Deck != _deck) return;
        _tempo.Apply(e);
        foreach (var name in _tempoNames) Notify(name);
    }

    void IEventHandler<DeckLoopChanged>.Handle(in DeckLoopChanged e)
    {
        if (e.Deck != _deck) return;
        _isLooping = e.Active;
        _loopStartSec = e.StartSec;
        _loopEndSec = e.EndSec;
        Notify(nameof(IsLooping));
        Notify(nameof(LoopStartSec));
        Notify(nameof(LoopEndSec));
    }

    void IEventHandler<DeckEditChanged>.Handle(in DeckEditChanged e)
    {
        if (e.Deck != _deck) return;
        _editOpen = e.EditOpen;
        _gridEditActive = e.GridEditActive;
        _isGridNudged = e.IsGridNudged;
        Notify(nameof(EditOpen));
        Notify(nameof(GridTuneActive));
        Notify(nameof(GridEditActive));
        Notify(nameof(IsGridNudged));
    }

    void IEventHandler<DeckMarkersChanged>.Handle(in DeckMarkersChanged e)
    {
        if (e.Deck != _deck) return;
        _markerSecs = e.Markers;
        Notify(nameof(MarkerSecs));
    }

    void IEventHandler<DeckMixChanged>.Handle(in DeckMixChanged e)
    {
        if (e.Deck != _deck) return;
        _mix = e;
        foreach (var name in _mixNames) Notify(name);
    }

    void IEventHandler<DeckPlayStateChanged>.Handle(in DeckPlayStateChanged e)
    {
        if (e.Deck != _deck) return;
        var wasPlaying = IsPlaying;
        var stateChanged = e.Phase != _playState;
        _playState = e.Phase;
        _endFlashOn = e.EndFlashOn;
        if (wasPlaying != IsPlaying) Notify(nameof(IsPlaying));
        if (stateChanged) Notify(nameof(PlayState));
        Notify(nameof(DiscRingOpacity));
    }

    void IEventHandler<StemMuteChanged>.Handle(in StemMuteChanged e)
    {
        if (e.Deck != _deck || !_chips.SetActive(e.Stem, !e.Muted)) return;
        Notify(_stemActiveNames[e.Stem]);
        Notify(_stemOpacityNames[e.Stem]);
    }

    // A knob sweep sends many levels; the chip only repaints when its opacity actually moves (the App
    // already drops sub-0.0001 changes before publishing).
    void IEventHandler<StemLevelChanged>.Handle(in StemLevelChanged e)
    {
        if (e.Deck == _deck && _chips.SetLevel(e.Stem, e.Level)) Notify(_stemOpacityNames[e.Stem]);
    }

    void IEventHandler<EchoChanged>.Handle(in EchoChanged e)
    {
        if (e.Deck != _deck) return;
        _echoActive = e.On;
        Notify(nameof(EchoActive));
    }

    void IEventHandler<HeadphoneCueChanged>.Handle(in HeadphoneCueChanged e)
    {
        if (e.Deck != _deck) return;
        _cueActive = e.On;
        Notify(nameof(CueActive));
    }

    // ---- Grid, loop ---------------------------------------------------------------------------
    /// <summary>True after the user has tapped at least one nudge on this deck's grid. The waveform uses
    /// this to recolour the loop band.</summary>
    public bool IsGridNudged => _isGridNudged;

    public bool GridEditActive => _gridEditActive;

    /// <summary>True while a beat-loop is engaged on this deck.</summary>
    public bool IsLooping => _isLooping;

    /// <summary>Loop-in point in seconds, or null when not looping. Bound to the waveform's loop band.</summary>
    public double? LoopStartSec => _isLooping ? _loopStartSec : null;

    /// <summary>Loop-out point in seconds, or null when not looping.</summary>
    public double? LoopEndSec => _isLooping ? _loopEndSec : null;

    // ---- Transport ----------------------------------------------------------------------------
    public DeckTrack? LoadedTrack => _loadedTrack;

    public bool IsPlaying => _playState != PlayPhase.Stopped;

    /// <summary>Transport state: Stopped when not playing, Ending in the last stretch, else Playing.</summary>
    public PlayPhase PlayState => _playState;

    /// <summary>Disc-ring opacity: solid (1.0) normally; flashes 1.0 / 0.35 in sync with the controller
    /// light while the track is Ending (both follow the App's frame-clock flash phase, carried on
    /// <see cref="DeckPlayStateChanged"/>). Bound by the DiscRing in the view.</summary>
    public double DiscRingOpacity =>
        _playState == PlayPhase.Ending ? (_endFlashOn ? 1.0 : 0.35) : 1.0;

    /// <summary>Playhead as a fraction of the track, 0 to 1.</summary>
    public double PlayPosition => _playPosition;

    public Avalonia.Media.IBrush DiscRingBrush => _ring.Brush;

    /// <summary>Rotation angle (degrees) for the deck disc overlay. One revolution per bar (4 beats):
    /// matches the feel of a vinyl turntable at the song's tempo.</summary>
    public double DiscAngle
    {
        get
        {
            if (Analysis is not { HasBasic: true, Bpm: > 0 } a) return 0;
            double secondsPerBar = 60.0 / a.Bpm * 4;
            return (_playbackSeconds / secondsPerBar) * 360.0;
        }
    }

    // ---- Analysis-derived display (see IDeckAnalysisDisplay) ----------------------------------
    public DeckAnalysis? Analysis => _display.Analysis;
    public WaveformPeaks Peaks => _display.Peaks;
    public WaveformPeaks GridPeaks => _display.GridPeaks;
    public IReadOnlyList<VocalRegion>? VocalRegions => _display.VocalRegions;
    public double[] BeatTimes => _display.BeatTimes;
    public double[] DownbeatTimes => _display.DownbeatTimes;
    public bool HasAnalysis => _display.HasAnalysis;
    public bool HasStems => _display.HasStems;
    public bool HasKey => _display.HasKey;
    public string Camelot => _display.Camelot;
    public Avalonia.Media.IBrush KeyBrush => _display.KeyBrush;

    /// <summary>The three glow opacities behind the BPM, smoothed from the band energies at the playhead.
    /// The same instance always; it raises its own <c>Changed</c> when a glow visibly moves.</summary>
    public IDiscBloomLevels Bloom => _bloom;

    public float EqLow => _display.LowAt(_playPosition);
    public float EqMid => _display.MidAt(_playPosition);
    public float EqHigh => _display.HighAt(_playPosition);

    /// <summary>Marker positions on the loaded track, in seconds, rendered as flags on the waveform.</summary>
    public double[] MarkerSecs => _markerSecs;

    // ---- Song sections (see IDeckSectionMap) --------------------------------------------------
    public IReadOnlyList<DeckSection> Sections => _sectionMap.Sections;
    public DeckPhraseGrid PhraseGrid => _sectionMap.PhraseGrid;
    public double FirstDownbeatSec => _sectionMap.FirstDownbeatSec;
    public double BarPeriodSec => _sectionMap.BarPeriodSec;
    public int TotalBars => _sectionMap.TotalBars;
    public bool HasSections => _sectionMap.HasSections;
    public bool SectionMapEnabled => _sectionMap.Enabled;
    public bool SectionMapVisible => _sectionMap.Visible;

    // ---- BPM and tempo display (see IDeckTempoDisplay) ----------------------------------------
    public double SourceBpm => _tempo.SourceBpm;
    public double BpmMultiplier => _tempo.BpmMultiplier;
    public double EffectiveBpm => _tempo.EffectiveBpm;
    public bool HasBpm => _tempo.HasBpm;
    public bool IsTempoShifted => _tempo.IsTempoShifted;
    public bool WasMagnetAdjusted => _tempo.WasMagnetAdjusted;
    public bool ShowOriginalBpm => _tempo.ShowOriginalBpm;
    public string BpmDisplay => _tempo.BpmDisplay;
    public string BpmDisplayShort => _tempo.BpmDisplayShort;
    public string OriginalBpmDisplay => _tempo.OriginalBpmDisplay;
    public string OriginalBpmShort => _tempo.OriginalBpmShort;
    public string TempoRangeDisplay => _tempo.TempoRangeDisplay;

    // ---- Tune editor (swing-out from the BPM) -------------------------------------------------
    public bool EditOpen => _editOpen;

    /// <summary>True while the tune editor is open: the only state in which the keyboard tempo/grid keys
    /// work and the waveform grid is emphasised.</summary>
    public bool GridTuneActive => _editOpen;

    /// <summary>Live playback speed multiplier (1.0 = unity). Bound to the waveform so its visual width
    /// compresses/stretches with the tempo fader and the half/double button.</summary>
    public double PlaybackSpeed => _playbackSpeed;

    /// <summary>Seconds of the track played so far, as the App last reported (not notified: read it when needed).</summary>
    public double PlaybackSeconds => _playbackSeconds;

    // ---- Load ---------------------------------------------------------------------------------
    public DeckLoadState LoadState => _loadState;

    /// <summary>Opacity for the deck's Info component (disc, title, BPM, key, stem chips): dimmed while
    /// the track is still loading, full once <see cref="DeckLoadState.Loaded"/>. The waveform component
    /// stays at full opacity throughout.</summary>
    public double InfoOpacity => LoadState == DeckLoadState.Loaded ? 1.0 : LoadingInfoOpacity;

    /// <summary>Info opacity while a track is loading/idle (0.3 = 70% transparent).</summary>
    private const double LoadingInfoOpacity = 0.3;

    /// <summary>Re-emit theme-derived bindings after a theme switch.</summary>
    public void RefreshThemeBindings()
    {
        Notify(nameof(KeyBrush));
        _ring.Recompute(_playPosition);
        Notify(nameof(DiscRingBrush));
    }

    /// <summary>True when the deck is allowed to start playback: loaded AND basic analysis (beat grid) present.</summary>
    public bool CanPlay => _loadState == DeckLoadState.Loaded && HasAnalysis;

    /// <summary>The deck holds a track (during a load, still the previous one until the new samples land).</summary>
    public bool IsLoaded => _isLoaded;

    // ---- Stems (see IDeckChipLevels) ----------------------------------------------------------
    public bool VocalsActive => _chips.VocalsActive;
    public bool InstrumentalActive => _chips.InstrumentalActive;
    public bool DrumsActive => _chips.DrumsActive;
    public double DrumsChipOpacity => _chips.DrumsChipOpacity;
    public double VocalsChipOpacity => _chips.VocalsChipOpacity;
    public double InstrumentalChipOpacity => _chips.InstrumentalChipOpacity;
    public double StemChipMinOpacity => _chips.MinOpacity;

    // ---- Performance flags --------------------------------------------------------------------
    /// <summary>True while the user is actively turning the jog wheel on this deck. Drives the full-height
    /// green guide line.</summary>
    public bool IsScrubbing => _isScrubbing;

    /// <summary>True while a platter scratch is in flight on this deck.</summary>
    public bool IsScratching => _isScratching;

    /// <summary>Time of the beat that should glow green (magnetism active), or -1 = off.</summary>
    public double MagneticGlowSec => _magneticGlowSec;

    // ---- Mixer, cue, echo ---------------------------------------------------------------------
    /// <summary>Channel fader 0..1, or null until the fader is measured.</summary>
    public double? ChannelGain => _mix.GainKnown ? _mix.ChannelGain : null;

    /// <summary>False until the channel fader has been measured. The waveform hides the gain line and the
    /// mute tint until then: we don't render a value we haven't measured.</summary>
    public bool GainKnown => _mix.GainKnown;

    /// <summary>Headphone cue (PFL) membership.</summary>
    public bool CueActive => _cueActive;

    /// <summary>Combined channel x crossfade gain, 0..1 (0 when unmeasured). Used to draw the gain line on
    /// the waveform, but only when <see cref="GainKnown"/>.</summary>
    public double EffectiveGain => _mix.EffectiveGain;

    /// <summary>The deck's beat-synced echo on/off state.</summary>
    public bool EchoActive => _echoActive;

    /// <summary>True when the deck is measured AND effectively silent. Drives the red mute tint.</summary>
    public bool IsMuted => _mix.IsMuted;

    private void Notify([CallerMemberName] string? name = null)
    {
        if (PropertyChanged is not { } handler || name is null) return;
        if (!_args.TryGetValue(name, out var args))
            _args[name] = args = new PropertyChangedEventArgs(name);
        handler(this, args);
    }
}

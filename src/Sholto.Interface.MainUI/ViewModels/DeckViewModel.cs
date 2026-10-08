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
    private readonly ICommandSender _sender;
    private readonly IThemeContext _theme;
    // The one "no peaks" value, so Peaks/GridPeaks do not allocate per read.
    private readonly WaveformPeaks _noPeaks;
    // One PropertyChangedEventArgs per property name, so the per-frame notifications allocate nothing.
    private readonly Dictionary<string, PropertyChangedEventArgs> _args = [];

    // Cached so we don't allocate a fresh brush every 16 ms: that was Avalonia's binding system seeing a
    // "new" IBrush per frame and invalidating the whole disc.
    private readonly Avalonia.Media.SolidColorBrush _discRingBrush = new();
    private double _lastRingNotifyPos = -1;
    // The disc bloom's smoothing state; the same instance for the life of the view model.
    private readonly IDiscBloom _bloom;

    // ---- What the App last told us ------------------------------------------------------------
    private DeckTrack? _loadedTrack;
    private DeckLoadState _loadState = DeckLoadState.Idle;
    private bool _isLoaded;
    private DeckAnalysis? _analysis;
    private IReadOnlyList<DeckSection> _sections = [];
    private DeckPhraseGrid _phraseGrid = new(0, 8);
    private double _firstDownbeatSec;
    private double _barPeriodSec;
    private int _totalBars;

    private PlayPhase _playState = PlayPhase.Stopped;
    private bool _endFlashOn;
    private double _playPosition;
    private double _playbackSeconds;
    private double _playbackSpeed = 1.0;
    private bool _isScrubbing;
    private bool _isScratching;
    private double _magneticGlowSec = -1;

    private double _sourceBpm;
    private double _bpmMultiplier = 1.0;
    private double _effectiveBpm;
    private double _tempoRange = 0.06;
    private bool _isTempoShifted;
    private bool _wasMagnetAdjusted;

    private bool _isLooping;
    private double _loopStartSec;
    private double _loopEndSec;

    private bool _editOpen;
    private bool _gridEditActive;
    private bool _isGridNudged;

    private double[] _markerSecs = [];

    private bool _gainKnown;
    private double _channelGain;
    private double _effectiveGain;
    private bool _isMuted;

    private bool _drumsActive = true, _vocalsActive = true, _instrumentalActive = true;
    private double _drumsLevel = 1.0, _vocalsLevel = 1.0, _instrumentalLevel = 1.0;
    private bool _cueActive;
    private bool _echoActive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeckViewModel(
        int deck, IEventSubscriber subscriber, ICommandSender sender, IThemeContext theme,
        INoPeaksFactory peaksFactory, IDiscBloomFactory bloomFactory)
    {
        _deck = deck;
        _sender = sender;
        _noPeaks = peaksFactory.None();
        _bloom = bloomFactory.Create();
        _theme = theme;
        RecomputeRingColor();

        // Each subscribe replays the current state, so a view model built late shows the real picture.
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

    // ---- Actions: each one is a command ---------------------------------------------------------
    private Origin Ui(string control, string gesture) =>
        new(InterfaceIds.MainUI, $"deck{_deck + 1}.{control}", gesture, _deck);

    /// <summary>Click the BPM: open the tune editor over the disc, or close it if it is open.</summary>
    public void ToggleEditor() => _sender.Send(new ToggleTuneEditor(_deck, Ui("bpm", "click")));

    public void CloseEditor() => _sender.Send(new CloseTuneEditor(_deck, Ui("tune-editor", "close")));

    /// <summary>The editor's half button, and the ÷2 / ×2 / reset gestures.</summary>
    public void ChangeBpm(BpmMultiplierOp op) =>
        _sender.Send(new ChangeBpmMultiplier(_deck, op, Ui("tune-editor.bpm-multiplier", "click")));

    public void AdjustBpm(double delta) =>
        _sender.Send(new AdjustBpm(_deck, delta, Ui("tune-editor.bpm", "step")));

    public void NudgeGridFine(double seconds) =>
        _sender.Send(new NudgeGridFine(_deck, seconds, Ui("tune-editor.grid", "nudge")));

    public void NudgeGrid(int beats) =>
        _sender.Send(new NudgeGrid(_deck, beats, Ui("tune-editor.grid", "nudge-beat")));

    public void ResetToAnalysis() =>
        _sender.Send(new ResetDeckToAnalysis(_deck, Ui("tune-editor.reset", "click")));

    /// <summary>A waveform click while the grid-edit mode is on.</summary>
    public void ClickGrid(double seconds) => _sender.Send(new ClickGrid(_deck, seconds, Ui("waveform", "click")));

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

        // Recolour the ring in-place and notify only when the visible colour actually changes (every ~1 %
        // of track length = ~2 s of music). Avoids ~120 brush invalidations/sec across both decks.
        RecomputeRingColor();
        if (Math.Abs(_playPosition - _lastRingNotifyPos) > 0.01)
        {
            _lastRingNotifyPos = _playPosition;
            Notify(nameof(DiscRingBrush));
        }
    }

    void IEventHandler<DeckContentChanged>.Handle(in DeckContentChanged e)
    {
        if (e.Deck != _deck) return;
        var loadStateChanged = e.LoadState != _loadState;
        var loadedChanged = e.IsLoaded != _isLoaded;
        _loadedTrack = e.Track;
        _loadState = e.LoadState;
        _isLoaded = e.IsLoaded;
        _analysis = e.Analysis;

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
        Notify(nameof(Analysis));
        Notify(nameof(Peaks));
        Notify(nameof(GridPeaks));
        Notify(nameof(VocalRegions));
        Notify(nameof(BeatTimes));
        Notify(nameof(DownbeatTimes));
        Notify(nameof(HasAnalysis));
        Notify(nameof(HasStems));
        Notify(nameof(Camelot));
        Notify(nameof(HasKey));
        Notify(nameof(KeyBrush));
    }

    void IEventHandler<DeckSectionsChanged>.Handle(in DeckSectionsChanged e)
    {
        if (e.Deck != _deck) return;
        _sections = e.Sections;
        _phraseGrid = e.PhraseGrid;
        _firstDownbeatSec = e.FirstDownbeatSec;
        _barPeriodSec = e.BarPeriodSec;
        _totalBars = e.TotalBars;
        Notify(nameof(Sections));
        Notify(nameof(PhraseGrid));
        Notify(nameof(FirstDownbeatSec));
        Notify(nameof(BarPeriodSec));
        Notify(nameof(TotalBars));
        Notify(nameof(HasSections));
        Notify(nameof(SectionMapVisible));
    }

    void IEventHandler<DeckTempoChanged>.Handle(in DeckTempoChanged e)
    {
        if (e.Deck != _deck) return;
        _sourceBpm = e.SourceBpm;
        _bpmMultiplier = e.BpmMultiplier;
        _effectiveBpm = e.EffectiveBpm;
        _tempoRange = e.TempoRange;
        _isTempoShifted = e.IsTempoShifted;
        _wasMagnetAdjusted = e.WasMagnetAdjusted;

        Notify(nameof(SourceBpm));
        Notify(nameof(HasBpm));
        Notify(nameof(BpmMultiplier));
        Notify(nameof(EffectiveBpm));
        Notify(nameof(BpmDisplay));
        Notify(nameof(BpmDisplayShort));
        Notify(nameof(OriginalBpmDisplay));
        Notify(nameof(OriginalBpmShort));
        Notify(nameof(IsTempoShifted));
        Notify(nameof(WasMagnetAdjusted));
        Notify(nameof(ShowOriginalBpm));
        Notify(nameof(TempoRangeDisplay));
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
        _gainKnown = e.GainKnown;
        _channelGain = e.ChannelGain;
        _effectiveGain = e.EffectiveGain;
        _isMuted = e.IsMuted;
        Notify(nameof(ChannelGain));
        Notify(nameof(GainKnown));
        Notify(nameof(EffectiveGain));
        Notify(nameof(IsMuted));
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
        if (e.Deck != _deck) return;
        var active = !e.Muted;
        switch (e.Stem)
        {
            case 0: _drumsActive = active; Notify(nameof(DrumsActive)); Notify(nameof(DrumsChipOpacity)); break;
            case 1: _vocalsActive = active; Notify(nameof(VocalsActive)); Notify(nameof(VocalsChipOpacity)); break;
            case 2: _instrumentalActive = active; Notify(nameof(InstrumentalActive)); Notify(nameof(InstrumentalChipOpacity)); break;
        }
    }

    // A knob sweep sends many levels; the chip only repaints when its opacity actually moves (the App
    // already drops sub-0.0001 changes before publishing).
    void IEventHandler<StemLevelChanged>.Handle(in StemLevelChanged e)
    {
        if (e.Deck != _deck) return;
        switch (e.Stem)
        {
            case 0: SetLevel(ref _drumsLevel, _drumsActive, e.Level, nameof(DrumsChipOpacity)); break;
            case 1: SetLevel(ref _vocalsLevel, _vocalsActive, e.Level, nameof(VocalsChipOpacity)); break;
            case 2: SetLevel(ref _instrumentalLevel, _instrumentalActive, e.Level, nameof(InstrumentalChipOpacity)); break;
        }
    }

    private void SetLevel(ref double field, bool active, double level, string property)
    {
        var before = ChipOpacity(field, active);
        field = level;
        if (ChipOpacity(field, active) != before) Notify(property);
    }

    private double ChipOpacity(double level, bool active) =>
        !active ? 1.0 : StemChipMinOpacity + (1.0 - StemChipMinOpacity) * Math.Clamp(level, 0.0, 1.0);

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

    /// <summary>Outer ring colour: green (0 %) -> yellow (50 %) -> orange (75 %) -> red (100 %). One cached
    /// brush whose Color is mutated in place, and only when it changes: no allocation per frame.</summary>
    public Avalonia.Media.IBrush DiscRingBrush => _discRingBrush;

    private void RecomputeRingColor()
    {
        var ring = _theme.Current.Ring;
        (byte r, byte g, byte b) green  = (ring.Start.R, ring.Start.G, ring.Start.B);
        (byte r, byte g, byte b) yellow = (ring.Mid.R, ring.Mid.G, ring.Mid.B);
        (byte r, byte g, byte b) orange = (ring.Late.R, ring.Late.G, ring.Late.B);
        (byte r, byte g, byte b) red    = (ring.End.R, ring.End.G, ring.End.B);
        double p = Math.Clamp(_playPosition, 0, 1);

        (byte r, byte g, byte b) Lerp((byte r, byte g, byte b) a, (byte r, byte g, byte b) b, double t) =>
            ((byte)(a.r + (b.r - a.r) * t),
             (byte)(a.g + (b.g - a.g) * t),
             (byte)(a.b + (b.b - a.b) * t));

        var c = p switch
        {
            < 0.5  => Lerp(green,  yellow, p / 0.5),
            < 0.75 => Lerp(yellow, orange, (p - 0.5) / 0.25),
            _      => Lerp(orange, red,    (p - 0.75) / 0.25),
        };
        // Assign only on a real change: Avalonia's Color setter allocates, and the colour moves only near
        // the end of a track, on load, or on theme change.
        var next = Avalonia.Media.Color.FromRgb(c.r, c.g, c.b);
        if (_discRingBrush.Color != next)
            _discRingBrush.Color = next;
    }

    /// <summary>Rotation angle (degrees) for the deck disc overlay. One revolution per bar (4 beats):
    /// matches the feel of a vinyl turntable at the song's tempo.</summary>
    public double DiscAngle
    {
        get
        {
            if (_analysis is not { HasBasic: true, Bpm: > 0 } a) return 0;
            double secondsPerBar = 60.0 / a.Bpm * 4;
            return (_playbackSeconds / secondsPerBar) * 360.0;
        }
    }

    // ---- Analysis-derived display -------------------------------------------------------------
    /// <summary>The loaded track's analysis as of the last content event: an immutable snapshot, replaced
    /// each time an analysis step lands; null until the first content event.</summary>
    public DeckAnalysis? Analysis => _analysis;

    /// <summary>Waveform peaks for rendering: always the mixed frequency-band peaks (low / mid / high) from
    /// basic analysis. The silhouette is the full mix and does NOT change when stems are muted: stem state
    /// is shown by the DRMS / VOX / INST chips and the audio, so the arrangement stays recognizable at a
    /// glance.</summary>
    public WaveformPeaks Peaks => _analysis?.Peaks ?? _noPeaks;

    /// <summary>Always the basic mixed peaks (or none until basic analysis lands). Used by
    /// <see cref="Controls.WaveformControl"/> as the time-mapping reference so the beatgrid keeps scrolling
    /// even when every stem is muted.</summary>
    public WaveformPeaks GridPeaks => _analysis?.Peaks ?? _noPeaks;

    /// <summary>Vocal-presence regions derived from the isolated vocal stem, or null until stems land.</summary>
    public IReadOnlyList<VocalRegion>? VocalRegions => _analysis?.VocalRegions;

    /// <summary>Marker positions on the loaded track, in seconds, rendered as flags on the waveform.</summary>
    public double[] MarkerSecs => _markerSecs;

    /// <summary>Phrase-aware sections in bars; empty until basic analysis lands. Turn a bar into seconds
    /// with <see cref="FirstDownbeatSec"/> + bar * <see cref="BarPeriodSec"/>.</summary>
    public IReadOnlyList<DeckSection> Sections => _sections;

    /// <summary>Phrase lines every <c>PhraseBars</c> bars from <c>PhaseBar</c>.</summary>
    public DeckPhraseGrid PhraseGrid => _phraseGrid;

    /// <summary>Time of bar 0.</summary>
    public double FirstDownbeatSec => _firstDownbeatSec;

    /// <summary>Seconds per bar; 0 when the track has no grid.</summary>
    public double BarPeriodSec => _barPeriodSec;

    /// <summary>Whole bars in the track.</summary>
    public int TotalBars => _totalBars;

    /// <summary>Sections exist and there is a bar grid to place them on.</summary>
    public bool HasSections => _sections.Count > 0 && _barPeriodSec > 0;

    /// <summary>Feature-flag gate for the section map (from <c>FeatureOptions</c>). Set once at
    /// construction by the factory.</summary>
    public bool SectionMapEnabled { get; init; }

    /// <summary>The section-map strip is shown only when the feature flag is on AND sections have been
    /// analysed. Bound by the minimap's <c>IsVisible</c>.</summary>
    public bool SectionMapVisible => SectionMapEnabled && HasSections;

    // ---- Band energy at the playhead (drives the disc bloom) ----------------------------------
    // Coarse frequency content at the playhead, straight from the band peaks: no FFT, no extra buffers.

    /// <summary>The three glow opacities behind the BPM, smoothed from the band energies at the playhead.
    /// The same instance always; it raises its own <c>Changed</c> when a glow visibly moves.</summary>
    public IDiscBloomLevels Bloom => _bloom;

    public float EqLow  => BandAt(p => p.Low);
    public float EqMid  => BandAt(p => p.Mid);
    public float EqHigh => BandAt(p => p.High);

    private float BandAt(Func<WaveformPeaks, float[]> pick)
    {
        var pk = _analysis?.Peaks;
        if (pk is null || pk.Min.Length == 0) return 0f;
        var arr = pick(pk);
        if (arr.Length != pk.Min.Length) return 0f;
        int col = Math.Clamp((int)(_playPosition * pk.Min.Length), 0, pk.Min.Length - 1);
        return arr[col];
    }

    public double[] BeatTimes => _analysis?.BeatTimes ?? [];
    public double[] DownbeatTimes => _analysis?.DownbeatTimes ?? [];

    // ---- BPM display --------------------------------------------------------------------------
    /// <summary>Source BPM (from analysis), unscaled.</summary>
    public double SourceBpm => _sourceBpm;

    /// <summary>User-applied multiplier (half / double).</summary>
    public double BpmMultiplier => _bpmMultiplier;

    // ---- Tune editor (swing-out from the BPM) -------------------------------------------------
    public bool EditOpen => _editOpen;

    /// <summary>True while the tune editor is open: the only state in which the keyboard tempo/grid keys
    /// work and the waveform grid is emphasised.</summary>
    public bool GridTuneActive => _editOpen;

    /// <summary>Source BPM x user multiplier x current playback speed: what the user actually hears.</summary>
    public double EffectiveBpm => _effectiveBpm;

    /// <summary>Live playback speed multiplier (1.0 = unity). Bound to the waveform so its visual width
    /// compresses/stretches with the tempo fader and the half/double button.</summary>
    public double PlaybackSpeed => _playbackSpeed;

    /// <summary>Seconds of the track played so far, as the App last reported (not notified: read it when needed).</summary>
    public double PlaybackSeconds => _playbackSeconds;

    public string BpmDisplay =>
        SourceBpm > 0 ? $"{EffectiveBpm:F1} BPM" : "";

    /// <summary>Just the number, no "BPM" suffix: used inside the disc.</summary>
    public string BpmDisplayShort =>
        SourceBpm > 0 ? $"{EffectiveBpm:F1}" : "";

    public bool HasBpm => SourceBpm > 0;

    // ---- Load ---------------------------------------------------------------------------------
    public DeckLoadState LoadState => _loadState;

    /// <summary>Opacity for the deck's Info component (disc, title, BPM, key, stem chips): dimmed while
    /// the track is still loading, full once <see cref="DeckLoadState.Loaded"/>. The waveform component
    /// stays at full opacity throughout.</summary>
    public double InfoOpacity => LoadState == DeckLoadState.Loaded ? 1.0 : LoadingInfoOpacity;

    /// <summary>Info opacity while a track is loading/idle (0.3 = 70% transparent).</summary>
    private const double LoadingInfoOpacity = 0.3;

    public bool HasAnalysis => _analysis?.HasBasic == true;

    /// <summary>True once Demucs stems have landed; until then the stem chip row hides.</summary>
    public bool HasStems => _analysis?.HasStems == true;

    private KeyRef? LoadedKey => _analysis?.Key;

    /// <summary>Camelot code for the loaded track (e.g. "8B"), or empty if key analysis hasn't completed.</summary>
    public string Camelot => LoadedKey?.ToCamelot() ?? "";

    public bool HasKey => LoadedKey is not null;

    /// <summary>Avalonia brush coloured by the Camelot key: used to tint the deck's key chip so the same
    /// colour shows here and in the library row. Pulls hue/sat/lightness from the active theme's
    /// CamelotPalette so theme switches retone live.</summary>
    public Avalonia.Media.IBrush KeyBrush
    {
        get
        {
            if (LoadedKey is not { } key) return Avalonia.Media.Brushes.Transparent;
            var p = _theme.Current.CamelotPalette;
            return p.KeyBrush(key);
        }
    }

    /// <summary>Re-emit theme-derived bindings after a theme switch.</summary>
    public void RefreshThemeBindings()
    {
        Notify(nameof(KeyBrush));
        RecomputeRingColor();
        Notify(nameof(DiscRingBrush));
    }

    // ---- Tempo --------------------------------------------------------------------------------
    /// <summary>True only when the tempo FADER has moved off-centre.</summary>
    public bool IsTempoShifted => _isTempoShifted;

    public bool WasMagnetAdjusted => _wasMagnetAdjusted;

    /// <summary>Should the "original BPM" side chip be popped out? True when the fader is meaningfully
    /// shifted OR when magnet snap just adjusted us. XAML's bpm-chip-top/bottom styles bind to this.</summary>
    public bool ShowOriginalBpm => IsTempoShifted || WasMagnetAdjusted;

    /// <summary>"Original" = source x half/double override, but without the live tempo-fader shift.</summary>
    public string OriginalBpmDisplay =>
        SourceBpm > 0 ? $"{(SourceBpm * BpmMultiplier):F1} BPM" : "";

    public string OriginalBpmShort =>
        SourceBpm > 0 ? $"{(SourceBpm * BpmMultiplier):F1}" : "";

    /// <summary>"+-6%" / "+-10%" / "+-16%" / "WIDE": the current tempo-range mode.</summary>
    public string TempoRangeDisplay =>
        _tempoRange >= 0.99 ? "WIDE" : $"±{_tempoRange * 100:F0}%";

    /// <summary>True when the deck is allowed to start playback: loaded AND basic analysis (beat grid) present.</summary>
    public bool CanPlay => _loadState == DeckLoadState.Loaded && HasAnalysis;

    /// <summary>The deck holds a track (during a load, still the previous one until the new samples land).</summary>
    public bool IsLoaded => _isLoaded;

    // ---- Stems --------------------------------------------------------------------------------
    public bool VocalsActive => _vocalsActive;

    public bool InstrumentalActive => _instrumentalActive;

    public bool DrumsActive => _drumsActive;

    /// <summary>Chip opacity per stem: the level (0..1) mapped to 0.3..1 so a turned-down stem stays readable.
    /// A muted chip stays at 1 so its hollow look is distinct from a low level.</summary>
    public double DrumsChipOpacity => ChipOpacity(_drumsLevel, _drumsActive);

    public double VocalsChipOpacity => ChipOpacity(_vocalsLevel, _vocalsActive);

    public double InstrumentalChipOpacity => ChipOpacity(_instrumentalLevel, _instrumentalActive);

    /// <summary>Opacity of a stem chip turned fully down (from <c>DeckViewOptions</c>). Set once at construction by
    /// the factory.</summary>
    public double StemChipMinOpacity { get; init; } = 0.3;

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
    public double? ChannelGain => _gainKnown ? _channelGain : null;

    /// <summary>False until the channel fader has been measured. The waveform hides the gain line and the
    /// mute tint until then: we don't render a value we haven't measured.</summary>
    public bool GainKnown => _gainKnown;

    /// <summary>Headphone cue (PFL) membership.</summary>
    public bool CueActive => _cueActive;

    /// <summary>Combined channel x crossfade gain, 0..1 (0 when unmeasured). Used to draw the gain line on
    /// the waveform, but only when <see cref="GainKnown"/>.</summary>
    public double EffectiveGain => _effectiveGain;

    /// <summary>The deck's beat-synced echo on/off state.</summary>
    public bool EchoActive => _echoActive;

    /// <summary>True when the deck is measured AND effectively silent. Drives the red mute tint.</summary>
    public bool IsMuted => _isMuted;

    private void Notify([CallerMemberName] string? name = null)
    {
        if (PropertyChanged is not { } handler || name is null) return;
        if (!_args.TryGetValue(name, out var args))
            _args[name] = args = new PropertyChangedEventArgs(name);
        handler(this, args);
    }
}

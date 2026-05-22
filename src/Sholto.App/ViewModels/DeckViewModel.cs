using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Audio;
using Sholto.Analysis;
using Sholto.App.Theming;
using Sholto.Library;

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

public sealed class DeckViewModel : INotifyPropertyChanged
{
    private readonly DeckPlayer _player;
    private Track? _loadedTrack;
    private bool _isPlaying;
    private double _playPosition;
    private DeckLoadState _loadState = DeckLoadState.Idle;
    // Tracks which TrackAnalysis instance we're currently subscribed to so we
    // can unsubscribe from it when _player.Analysis gets replaced (each
    // BeginLoad creates a fresh TrackAnalysis on the player).
    private TrackAnalysis? _subscribedAnalysis;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeckViewModel(DeckPlayer player)
    {
        _player = player;
        RebindAnalysisSubscription();
    }

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
            _subscribedAnalysis.StemsReady      -= OnStemsReady;
            _subscribedAnalysis.StemPeaksReady  -= OnStemPeaksReady;
        }
        _subscribedAnalysis = _player.Analysis;
        _subscribedAnalysis.BasicReady      += OnBasicReady;
        _subscribedAnalysis.KeyReady        += OnKeyReady;
        _subscribedAnalysis.StemsReady      += OnStemsReady;
        _subscribedAnalysis.StemPeaksReady  += OnStemPeaksReady;
    }

    // Each per-type handler only re-notifies the bindings that DEPEND on that
    // analysis type. Cheaper than the old "fire everything on AnalysisUpdated"
    // pattern and clearer about cause → effect when reading the code.
    private void OnBasicReady(BasicAnalysis _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Notify(nameof(Analysis));
            Notify(nameof(HasAnalysis));
            Notify(nameof(HasBpm));
            Notify(nameof(SourceBpm));
            Notify(nameof(BpmDisplay));
            Notify(nameof(BpmDisplayShort));
            Notify(nameof(EffectiveBpm));
            Notify(nameof(Peaks));
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

    private void OnStemPeaksReady(StemPeaks _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(nameof(Peaks)));

    public DeckPlayer Player => _player;

    public Track? LoadedTrack
    {
        get => _loadedTrack;
        private set { _loadedTrack = value; Notify(); }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set { _isPlaying = value; Notify(); }
    }

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
    /// Waveform peaks for rendering. When per-stem peaks are available, returns
    /// a merge across the currently-active stems so toggling DRMS / VOX / INST
    /// visibly shrinks or grows the waveform. Otherwise falls back to the mixed
    /// peaks from basic analysis.
    /// </summary>
    public WaveformPeaks Peaks
    {
        get
        {
            var stems = Analysis.Get<StemPeaks>();
            if (stems is null) return Analysis.Basic?.Peaks ?? WaveformPeaks.Empty;
            return MergeActiveStemPeaks(stems);
        }
    }

    /// <summary>Combine per-stem peaks across the currently-active stems. At each
    /// peak slot: take the min of Mins / max of Maxes for the outline, and the
    /// max of each band (Low / Mid / High) for the colour gradient. Returns the
    /// mixed peaks if nothing is active so the waveform doesn't go blank
    /// (the user can still see the song's shape while everything's muted).</summary>
    private WaveformPeaks MergeActiveStemPeaks(StemPeaks s)
    {
        // "Instrumental" maps to Bass + Other internally — same convention as
        // StemMixDataProvider / SetStemGroup, so the audio you hear matches the
        // waveform you see.
        var sources = new List<WaveformPeaks>(4);
        if (_drumsActive)        sources.Add(s.Drums);
        if (_vocalsActive)       sources.Add(s.Vocals);
        if (_instrumentalActive) { sources.Add(s.Bass); sources.Add(s.Other); }
        if (sources.Count == 0)  return Analysis.Basic?.Peaks ?? WaveformPeaks.Empty;

        int n = sources[0].Min.Length;
        if (n == 0) return WaveformPeaks.Empty;

        var min = new float[n];
        var max = new float[n];
        var lo  = new float[n];
        var mid = new float[n];
        var hi  = new float[n];
        foreach (var src in sources)
        {
            // Defensive: every per-stem WaveformPeaks should be the same length,
            // but guard against off-by-one truncation between decoders.
            int len = Math.Min(n, src.Min.Length);
            for (int i = 0; i < len; i++)
            {
                if (src.Min[i] < min[i]) min[i] = src.Min[i];
                if (src.Max[i] > max[i]) max[i] = src.Max[i];
                if (src.Low[i]  > lo[i])  lo[i]  = src.Low[i];
                if (src.Mid[i]  > mid[i]) mid[i] = src.Mid[i];
                if (src.High[i] > hi[i])  hi[i]  = src.High[i];
            }
        }
        return new WaveformPeaks(min, max, lo, mid, hi, sources[0].SamplesPerPeak);
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
            LoadStateChanged?.Invoke(value);
        }
    }

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
    /// the required shift falls outside <see cref="DeckPlayer.PitchRange"/>,
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

    /// <summary>"±6%" / "±10%" / "±16%" / "±50%" — the current pitch-range mode.</summary>
    public string PitchRangeDisplay => $"±{_player.PitchRange * 100:F0}%";

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
        // Reset per-stem mute state — a new track always starts with all stems
        // active. Without this, "vocals muted" from track 1 would silently
        // carry over to track 2 (audio resets via SwitchToStemMode, but the
        // UI toggles would show stale state).
        DrumsActive = true;
        VocalsActive = true;
        InstrumentalActive = true;
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
        Notify(nameof(Peaks));
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
    /// <see cref="DeckPlayer.LoadStreaming"/> so audio starts in ~100 ms, no
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

    // Stem state — controls the per-stem mute toggles AND the per-stem
    // waveform merge. Setters Notify(Peaks) so the waveform shrinks/grows in
    // real time as the user flips stems on and off. Default ON so a
    // freshly-loaded track shows all three filled.
    private bool _vocalsActive = true, _instrumentalActive = true, _drumsActive = true;
    public bool VocalsActive
    {
        get => _vocalsActive;
        set { if (_vocalsActive == value) return; _vocalsActive = value; Notify(); Notify(nameof(Peaks)); }
    }
    public bool InstrumentalActive
    {
        get => _instrumentalActive;
        set { if (_instrumentalActive == value) return; _instrumentalActive = value; Notify(); Notify(nameof(Peaks)); }
    }
    public bool DrumsActive
    {
        get => _drumsActive;
        set { if (_drumsActive == value) return; _drumsActive = value; Notify(); Notify(nameof(Peaks)); }
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
    private float _channelGain = 1.0f;
    private float _crossfadeGain = 1.0f;

    /// <summary>Channel fader 0..1 (the per-deck slider). Bound from UI + FLX-4 channel faders.</summary>
    public double ChannelGain
    {
        get => _channelGain;
        set
        {
            var v = (float)Math.Clamp(value, 0, 1);
            if (Math.Abs(v - _channelGain) < 0.001f) return;
            _channelGain = v;
            ApplyVolume();
            Notify();
        }
    }

    /// <summary>Set by MainViewModel when the crossfader moves; not bound from UI.</summary>
    internal void SetCrossfadeGain(float gain)
    {
        _crossfadeGain = Math.Clamp(gain, 0f, 1f);
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        _player.Volume = _channelGain * _crossfadeGain;
        Notify(nameof(EffectiveGain));
        Notify(nameof(IsMuted));
    }

    /// <summary>Combined channel × crossfade gain, 0..1. Used to draw the gain line on the waveform.</summary>
    public double EffectiveGain => _channelGain * _crossfadeGain;

    /// <summary>True when the deck is effectively silent (channel fader at 0 or fully crossed away).
    /// Drives the red mute tint over the deck area.</summary>
    public bool IsMuted => EffectiveGain < 0.001;

    public void SyncPlayPosition()
    {
        PlayPosition = _player.PlayPosition;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

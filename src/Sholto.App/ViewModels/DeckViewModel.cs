using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Audio;
using Sholto.Analysis;
using Sholto.Library;

namespace Sholto.App.ViewModels;

public sealed class DeckViewModel : INotifyPropertyChanged
{
    private readonly DeckPlayer _player;
    private Track? _loadedTrack;
    private bool _isPlaying;
    private double _playPosition;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeckViewModel(DeckPlayer player)
    {
        _player = player;
        _player.AnalysisUpdated += OnAnalysisUpdated;
    }

    private void OnAnalysisUpdated()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Notify(nameof(Analysis));
            Notify(nameof(Peaks));
            Notify(nameof(BeatTimes));
            Notify(nameof(DownbeatTimes));
            Notify(nameof(BpmDisplay));
            Notify(nameof(BpmDisplayShort));
            Notify(nameof(HasBpm));
        });
    }

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
    public WaveformPeaks Peaks => Analysis.Basic?.Peaks ?? WaveformPeaks.Empty;
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

    /// <summary>Forward the FLX-4 tempo fader to the player. Position 0..1, 0.5 = unity.</summary>
    public void SetTempoPosition(double pos)
    {
        _player.TempoPosition = pos;
        Notify(nameof(BpmDisplay));
        Notify(nameof(BpmDisplayShort));
        Notify(nameof(EffectiveBpm));
        Notify(nameof(IsTempoShifted));
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

    /// <summary>"Original" = source × half/double override, but without the live
    /// tempo-fader shift. That's the BPM the user thinks of as "the track's BPM"
    /// once they've corrected any madmom octave error.</summary>
    public string OriginalBpmDisplay =>
        SourceBpm > 0 ? $"{(SourceBpm * _bpmMultiplier):F1} BPM" : "";

    public string OriginalBpmShort =>
        SourceBpm > 0 ? $"{(SourceBpm * _bpmMultiplier):F1}" : "";

    /// <summary>"±6%" / "±10%" / "±16%" / "±50%" — the current pitch-range mode.</summary>
    public string PitchRangeDisplay => $"±{_player.PitchRange * 100:F0}%";

    public void LoadTrack(Track track, string filePath, float[] samples, double bpmMultiplier = 1.0)
    {
        _player.Load(filePath, samples, sampleRate: AudioFileDecoder.TargetSampleRate);
        LoadedTrack = track;
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

    public void TogglePlay()
    {
        _player.TogglePlay();
        IsPlaying = _player.IsPlaying;
    }

    public void Unload()
    {
        _player.Unload();
        LoadedTrack = null;
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

    // Stem state — UI hint only for now; actual Demucs separation + per-stem mute
    // will land later. Default ON so a freshly-loaded track shows all three filled.
    private bool _vocalsActive = true, _instrumentalActive = true, _drumsActive = true;
    public bool VocalsActive       { get => _vocalsActive;       set { if (_vocalsActive == value) return;       _vocalsActive = value;       Notify(); } }
    public bool InstrumentalActive { get => _instrumentalActive; set { if (_instrumentalActive == value) return; _instrumentalActive = value; Notify(); } }
    public bool DrumsActive        { get => _drumsActive;        set { if (_drumsActive == value) return;        _drumsActive = value;        Notify(); } }

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

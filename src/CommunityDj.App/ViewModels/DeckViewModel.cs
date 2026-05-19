using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityDj.Audio;
using CommunityDj.Analysis;
using CommunityDj.Library;

namespace CommunityDj.App.ViewModels;

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

    public double PlayPosition
    {
        get => _playPosition;
        set
        {
            _playPosition = value;
            Notify();
            Notify(nameof(DiscAngle));
            Notify(nameof(DiscRingBrush));
            Notify(nameof(IsNearEnd));
        }
    }

    /// <summary>True once the play position is past 90% — used to drive the end-of-track flash.</summary>
    public bool IsNearEnd => _playPosition >= 0.9;

    /// <summary>
    /// Outer ring colour: green (0%) → yellow (50%) → orange (75%) → red (100%).
    /// Linearly interpolated in RGB; rough but reads well at glance.
    /// </summary>
    public Avalonia.Media.IBrush DiscRingBrush
    {
        get
        {
            (byte r, byte g, byte b) green  = (0x34, 0xF0, 0x6F);
            (byte r, byte g, byte b) yellow = (0xFF, 0xD6, 0x3D);
            (byte r, byte g, byte b) orange = (0xFF, 0x8C, 0x2A);
            (byte r, byte g, byte b) red    = (0xFF, 0x3D, 0x4E);

            double p = Math.Clamp(_playPosition, 0, 1);
            (byte r, byte g, byte b) lerp(double a, (byte r, byte g, byte b) c1, (byte r, byte g, byte b) c2, double t) =>
                ((byte)(c1.r + (c2.r - c1.r) * t),
                 (byte)(c1.g + (c2.g - c1.g) * t),
                 (byte)(c1.b + (c2.b - c1.b) * t));

            (byte r, byte g, byte b) c = p switch
            {
                < 0.5 => lerp(p, green,  yellow, p / 0.5),
                < 0.75 => lerp(p, yellow, orange, (p - 0.5) / 0.25),
                _ => lerp(p, orange, red, (p - 0.75) / 0.25),
            };
            return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(c.r, c.g, c.b));
        }
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
            double playSeconds = _player.PositionFrames / 44100.0;
            double secondsPerBar = 60.0 / basic.Bpm * 4;
            return (playSeconds / secondsPerBar) * 360.0;
        }
    }

    public TrackAnalysis Analysis => _player.Analysis;
    public WaveformPeaks Peaks => Analysis.Basic?.Peaks ?? WaveformPeaks.Empty;
    public double[] BeatTimes => Analysis.Basic?.BeatTimes ?? [];
    public double[] DownbeatTimes => Analysis.Basic?.DownbeatTimes ?? [];

    public string BpmDisplay =>
        Analysis.Basic is { Bpm: > 0 } b ? $"{b.Bpm:F1} BPM" : "";

    /// <summary>Just the number, no "BPM" suffix — used inside the disc.</summary>
    public string BpmDisplayShort =>
        Analysis.Basic is { Bpm: > 0 } b ? $"{b.Bpm:F1}" : "";

    public bool HasBpm => Analysis.Basic is { Bpm: > 0 };

    public void LoadTrack(Track track, string filePath, float[] samples)
    {
        _player.Load(filePath, samples, sampleRate: 44100);
        LoadedTrack = track;
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
    public double PlaybackSeconds => _player.PositionFrames / 44100.0;

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

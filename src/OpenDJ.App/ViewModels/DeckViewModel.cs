using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenDJ.Audio;
using OpenDJ.Analysis;
using OpenDJ.Library;

namespace OpenDJ.App.ViewModels;

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

    public bool HasBpm => Analysis.Basic is { Bpm: > 0 };

    public void LoadTrack(Track track, string filePath, float[] samples)
    {
        _player.Load(filePath, samples, sampleRate: 44100);
        LoadedTrack = track;
        IsPlaying = false;
        PlayPosition = 0;
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

    public void SyncPlayPosition()
    {
        PlayPosition = _player.PlayPosition;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

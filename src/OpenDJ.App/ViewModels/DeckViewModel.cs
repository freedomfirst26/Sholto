using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenDJ.Audio;
using OpenDJ.Audio.Analysis;
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
        set { _playPosition = value; Notify(); }
    }

    public TrackAnalysis Analysis => _player.Analysis;
    public WaveformPeaks Peaks => Analysis.Basic?.Peaks ?? WaveformPeaks.Empty;

    public string BpmDisplay =>
        Analysis.Basic is { Bpm: > 0 } b ? $"{b.Bpm:F1} BPM" : "";

    public void LoadTrack(Track track, float[] samples)
    {
        _player.Load(samples, sampleRate: 44100);
        LoadedTrack = track;
        IsPlaying = false;
        PlayPosition = 0;
        Notify(nameof(Analysis));
        Notify(nameof(Peaks));
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

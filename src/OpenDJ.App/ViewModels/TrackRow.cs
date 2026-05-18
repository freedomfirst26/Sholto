using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenDJ.Library;

namespace OpenDJ.App.ViewModels;

/// <summary>
/// One row in the track list. Wraps a Track plus the slow-to-compute fields
/// (BPM, key, etc.) so the row can "uplift" as analyses arrive.
/// </summary>
public sealed class TrackRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Track Track { get; }
    public string FilePath => Track.FilePath;
    public string Title => Track.Title;
    public string Artist => Track.Artist;
    public TimeSpan Duration => Track.Duration;

    public TrackRow(Track track) { Track = track; }

    private double? _bpm;
    public double? Bpm
    {
        get => _bpm;
        set { if (Math.Abs((_bpm ?? -1) - (value ?? -1)) < 0.05) return; _bpm = value; Notify(); Notify(nameof(BpmDisplay)); }
    }

    public string BpmDisplay => _bpm is { } b ? $"{b:F1}" : "";

    private string? _key;
    public string? Key
    {
        get => _key;
        set { if (_key == value) return; _key = value; Notify(); }
    }

    public string DurationDisplay => $"{(int)Duration.TotalMinutes:00}:{Duration.Seconds:00}";

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

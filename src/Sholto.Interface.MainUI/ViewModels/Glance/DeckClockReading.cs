using Avalonia.Media;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>What the header needs to know about one deck right now: whether it holds a track, whether it is
/// playing, the track's title and length, and how far into it the playhead is. The trailing facts feed the
/// deck slots: the key chip (<paramref name="Camelot"/>, <paramref name="KeyBrush"/>), the tempo heard
/// (<paramref name="Bpm"/>) and the bar grid the platter spins to (<paramref name="FirstDownbeatSeconds"/>,
/// <paramref name="BarPeriodSeconds"/>, 0 when the track has none).</summary>
public readonly record struct DeckClockReading(
    bool IsLoaded, bool IsPlaying, string? Title, double DurationSeconds, double PlaybackSeconds, double PlaybackSpeed,
    string Camelot = "", IBrush? KeyBrush = null, double Bpm = 0,
    double FirstDownbeatSeconds = 0, double BarPeriodSeconds = 0);

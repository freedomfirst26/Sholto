using Sholto.App.Analysis.Harmony;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>Conversions between the App's own types and the plain ones that cross the bus
/// (<see cref="TrackSummary"/>, <see cref="KeyRef"/>). Extension methods.</summary>
public static class SummaryMappingExtensions
{
    public static TrackSummary ToSummary(this Track track) =>
        new(track.FilePath, track.Title, track.Artist, track.Duration);

    public static Track ToTrack(this TrackSummary summary) =>
        new(summary.FilePath, summary.Title, summary.Artist, summary.Duration);

    public static KeyRef ToRef(this Key key) => new(key.PitchClass, key.IsMajor);

    public static KeyRef? ToRef(this Key? key) => key is { } k ? k.ToRef() : null;

    public static Key ToKey(this KeyRef key) => new(key.PitchClass, key.IsMajor);

    public static Key? ToKey(this KeyRef? key) => key is { } k ? k.ToKey() : null;
}

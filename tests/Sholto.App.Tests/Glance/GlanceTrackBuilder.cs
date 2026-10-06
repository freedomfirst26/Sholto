using Sholto.Data;

namespace Sholto.App.Tests.Glance;

/// <summary>Builds library rows for Glance tests from a Camelot code.</summary>
internal sealed class GlanceTrackBuilder
{
    // Pitch class by Camelot number for the major (B) and minor (A) rings: inverse of Key.CamelotNumber.
    private readonly int[] _majorPc = { 0, 11, 6, 1, 8, 3, 10, 5, 0, 7, 2, 9, 4 };
    private readonly int[] _minorPc = { 0, 8, 3, 10, 5, 0, 7, 2, 9, 4, 11, 6, 1 };

    public KeyRef Key(string camelot)
    {
        int n = int.Parse(camelot[..^1]);
        bool major = char.ToUpperInvariant(camelot[^1]) == 'B';
        return new KeyRef((major ? _majorPc : _minorPc)[n], major);
    }

    public TrackSummary Track(string name, string? key, double? bpm, bool played = false, string artist = "A", double multiplier = 1.0, params string[] tags) =>
        new($"/m/{name}.mp3", name, artist, TimeSpan.FromMinutes(4))
        {
            Bpm = bpm,
            BpmMultiplier = multiplier,
            MusicalKey = key is null ? null : Key(key),
            IsPlayed = played,
            Tags = tags,
        };
}

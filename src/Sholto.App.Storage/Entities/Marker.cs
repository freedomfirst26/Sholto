namespace Sholto.Storage.Entities;

/// <summary>A saved position on a track — our equivalent of a Rekordbox memory cue.
/// A track can have many. Rendered on the waveform and reachable for quick jumps;
/// In/Out markers can be chained across decks via <see cref="MarkerLink"/>.</summary>
internal sealed class Marker
{
    public int Id { get; set; }
    public Guid TrackId { get; set; }
    /// <summary>Position in track seconds.</summary>
    public double PositionSecs { get; set; }
    public MarkerKind Kind { get; set; } = MarkerKind.Memory;
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }

    public Track Track { get; set; } = null!;
}

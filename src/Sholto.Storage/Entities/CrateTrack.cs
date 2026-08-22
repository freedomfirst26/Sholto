namespace Sholto.Storage.Entities;

/// <summary>Join row placing a <see cref="Track"/> in a <see cref="Crate"/> at a
/// given <see cref="Position"/> (0-based order within that crate). A track may
/// appear in many crates; a crate holds many tracks.</summary>
public sealed class CrateTrack
{
    public int CrateId { get; set; }
    public Guid TrackId { get; set; }
    /// <summary>0-based order of this track within the crate.</summary>
    public int Position { get; set; }

    public Crate Crate { get; set; } = null!;
    public Track Track { get; set; } = null!;
}

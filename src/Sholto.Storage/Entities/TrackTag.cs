namespace Sholto.Storage.Entities;

public sealed class TrackTag
{
    public Guid TrackId { get; set; }
    public int TagId { get; set; }

    public Track Track { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

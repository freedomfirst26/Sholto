namespace Sholto.Storage.Entities;

public sealed class BasicAnalysisRecord
{
    public Guid TrackId { get; set; }
    public byte[] Data { get; set; } = null!;
    public long FileMtime { get; set; }
    public DateTime CreatedAt { get; set; }
    public Track Track { get; set; } = null!;
}

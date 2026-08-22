namespace Sholto.Storage.Entities;

public sealed class BpmOverride
{
    public Guid TrackId { get; set; }
    public double Multiplier { get; set; }
    public Track Track { get; set; } = null!;
}

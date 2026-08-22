namespace Sholto.Storage.Entities;

public sealed class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public ICollection<TrackTag> TrackTags { get; set; } = new List<TrackTag>();
}

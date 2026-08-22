namespace Sholto.Storage.Entities;

public sealed class Track
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Artist { get; set; } = null!;
    public long FileSize { get; set; }
    public long FileMtime { get; set; }
    public double DurationSecs { get; set; }

    public BasicAnalysisRecord? BasicAnalysis { get; set; }
    public KeyAnalysisRecord? KeyAnalysis { get; set; }
    public BpmOverride? BpmOverride { get; set; }
    public GridAdjustment? GridAdjustment { get; set; }

    public ICollection<TrackTag> TrackTags { get; set; } = new List<TrackTag>();
}

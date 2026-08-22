namespace Sholto.Storage.Entities;

/// <summary>
/// Per-track manual beatgrid correction. Kept SEPARATE from the immutable
/// madmom-detected <see cref="BasicAnalysisRecord"/> so the detection is
/// never destroyed — the grid the user sees is regenerated from
/// (BpmOverride ?? detectedBpm, OffsetSec). A missing row means "never
/// adjusted, use the detection as-is". Deleting the row resets the grid.
/// </summary>
public sealed class GridAdjustment
{
    public Guid TrackId { get; set; }

    /// <summary>Manually-corrected BPM. Null = use the detected BPM. Set when
    /// the user tunes the bar SPACING (grid drifts off the kicks over time).</summary>
    public double? BpmOverride { get; set; }

    /// <summary>Phase shift in seconds applied to the whole grid (beats AND
    /// downbeats together). Positive = grid moves later in time. Accumulated
    /// by the left/right nudge.</summary>
    public double OffsetSec { get; set; }

    public Track Track { get; set; } = null!;
}

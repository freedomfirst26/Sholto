namespace Sholto.App.Analysis.Stores;

/// <summary>
/// Persistence port for one track's manual beatgrid correction (BPM override +
/// phase offset). Lives here for the same reason as <see cref="IKeyAnalysisStore"/>
/// — see that type's doc comment.
/// </summary>
public interface IGridAdjustmentStore
{
    /// <summary>Returns (bpmOverride, offsetSec) for the track, or null if no
    /// adjustment has been saved.</summary>
    Task<(double? BpmOverride, double OffsetSec)?> TryGetAsync(string filePath);

    /// <summary>Upsert the adjustment for a track. Pass bpmOverride=null to mean
    /// "use detected BPM" while still recording a phase offset.</summary>
    Task PutAsync(string filePath, double? bpmOverride, double offsetSec);
}

namespace Sholto.Analysis.Stores;

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

/// <summary>The do-nothing store — always a miss, writes go nowhere.</summary>
public sealed class NullGridAdjustmentStore : IGridAdjustmentStore
{
    public static readonly NullGridAdjustmentStore Instance = new();
    private NullGridAdjustmentStore() { }
    public Task<(double? BpmOverride, double OffsetSec)?> TryGetAsync(string filePath) =>
        Task.FromResult<(double? BpmOverride, double OffsetSec)?>(null);
    public Task PutAsync(string filePath, double? bpmOverride, double offsetSec) => Task.CompletedTask;
}

/// <summary>Forwards to whatever <see cref="IGridAdjustmentStore"/> is currently
/// attached. See <see cref="SwitchableKeyAnalysisStore"/> for the full rationale —
/// same pattern, different cache.</summary>
public sealed class SwitchableGridAdjustmentStore : IGridAdjustmentStore
{
    private IGridAdjustmentStore _target = NullGridAdjustmentStore.Instance;

    public void Attach(IGridAdjustmentStore real) => _target = real;

    public Task<(double? BpmOverride, double OffsetSec)?> TryGetAsync(string filePath) => _target.TryGetAsync(filePath);
    public Task PutAsync(string filePath, double? bpmOverride, double offsetSec) => _target.PutAsync(filePath, bpmOverride, offsetSec);
}

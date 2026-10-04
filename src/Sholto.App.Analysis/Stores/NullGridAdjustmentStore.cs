namespace Sholto.App.Analysis.Stores;

/// <summary>The do-nothing store — always a miss, writes go nowhere.</summary>
public sealed class NullGridAdjustmentStore : IGridAdjustmentStore
{
    public Task<(double? BpmOverride, double OffsetSec)?> TryGetAsync(string filePath) =>
        Task.FromResult<(double? BpmOverride, double OffsetSec)?>(null);
    public Task PutAsync(string filePath, double? bpmOverride, double offsetSec) => Task.CompletedTask;
}

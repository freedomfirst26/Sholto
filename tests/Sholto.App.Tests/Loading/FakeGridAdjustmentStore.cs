using Sholto.App.Analysis.Stores;

namespace Sholto.App.Tests;

/// <summary>No grid adjustments are ever stored.</summary>
internal sealed class FakeGridAdjustmentStore : IGridAdjustmentStore
{
    public Task<(double? BpmOverride, double OffsetSec)?> TryGetAsync(string filePath) =>
        Task.FromResult<(double? BpmOverride, double OffsetSec)?>(null);

    public Task PutAsync(string filePath, double? bpmOverride, double offsetSec) => Task.CompletedTask;
}

namespace Sholto.App.Analysis.Stores;

/// <summary>
/// Forwards to the real <see cref="IGridAdjustmentStore"/> once the library database has
/// opened; every call awaits <paramref name="store"/> first. A database that failed to
/// open (the task yields null) means no persistence: reads miss, writes go nowhere.
/// </summary>
public sealed class DeferredGridAdjustmentStore(Task<IGridAdjustmentStore?> store) : IGridAdjustmentStore
{
    private readonly Task<IGridAdjustmentStore?> _store = store;

    public async Task<(double? BpmOverride, double OffsetSec)?> TryGetAsync(string filePath)
    {
        var s = await _store;
        return s is null ? null : await s.TryGetAsync(filePath);
    }

    public async Task PutAsync(string filePath, double? bpmOverride, double offsetSec)
    {
        var s = await _store;
        if (s is not null) await s.PutAsync(filePath, bpmOverride, offsetSec);
    }
}

using Sholto.App.Library.Markers;

namespace Sholto.App.Tests;

/// <summary>Markers held in memory, per track.</summary>
internal sealed class FakeMarkerService : IMarkerService
{
    private readonly Dictionary<Guid, List<double>> _markers = [];

    public Task<int> AddAsync(Guid trackId, double positionSec)
    {
        if (!_markers.TryGetValue(trackId, out var list)) _markers[trackId] = list = [];
        list.Add(positionSec);
        return Task.FromResult(list.Count);
    }

    public Task<IReadOnlyList<double>> ListPositionsAsync(Guid trackId) =>
        Task.FromResult<IReadOnlyList<double>>(_markers.TryGetValue(trackId, out var list) ? list.Order().ToList() : []);
}

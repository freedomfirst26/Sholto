using Sholto.App.Library.Crates;

namespace Sholto.TestSupport;

/// <summary>Crates held in memory, recording what was filed where.</summary>
internal sealed class FakeCrateService : ICrateService
{
    private readonly Dictionary<int, string> _names = [];
    private readonly Dictionary<int, HashSet<Guid>> _members = [];

    public Task<int> CreateAsync(string name)
    {
        var existing = _names.FirstOrDefault(kv => kv.Value == name);
        if (existing.Value is not null) return Task.FromResult(existing.Key);
        var id = _names.Count + 1;
        _names[id] = name;
        _members[id] = [];
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<CrateSummary>> SearchAsync(string query) =>
        Task.FromResult<IReadOnlyList<CrateSummary>>(
            _names.Select(kv => new CrateSummary(kv.Key, kv.Value, _members[kv.Key].Count)).ToList());

    public Task<bool> AddTrackAsync(int crateId, Guid trackId) => Task.FromResult(_members[crateId].Add(trackId));

    public Task<IReadOnlyList<Guid>> TrackIdsAsync(int crateId) =>
        Task.FromResult<IReadOnlyList<Guid>>(_members[crateId].ToList());

    /// <summary>Ids filed into the crate called <paramref name="name"/>, or empty when there is none.</summary>
    public IReadOnlyCollection<Guid> Members(string name)
    {
        var id = _names.FirstOrDefault(kv => kv.Value == name);
        if (id.Value is null) return Array.Empty<Guid>();
        return _members[id.Key];
    }
}

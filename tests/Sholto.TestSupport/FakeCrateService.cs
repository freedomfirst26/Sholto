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

    public Task<CrateMates> CrateMatesAsync(Guid trackId)
    {
        var crates = _names.Where(kv => kv.Value != CrateNames.AllTracks && _members[kv.Key].Contains(trackId)).ToList();
        return Task.FromResult(new CrateMates(
            crates.Select(kv => kv.Value).Order(StringComparer.Ordinal).ToList(),
            crates.SelectMany(kv => _members[kv.Key]).Where(id => id != Guid.Empty).ToHashSet()));
    }

    /// <summary>How many times <see cref="MembershipAsync"/> has been called.</summary>
    public int MembershipCalls { get; private set; }

    public Task<CrateMembership> MembershipAsync()
    {
        MembershipCalls++;
        var allTracks = _names.Where(kv => kv.Value == CrateNames.AllTracks).Select(kv => (int?)kv.Key).FirstOrDefault();
        var byCrate = _names
            .Where(kv => kv.Value != CrateNames.AllTracks)
            .ToDictionary(kv => kv.Key, kv => (IReadOnlySet<Guid>)_members[kv.Key].ToHashSet());
        return Task.FromResult(new CrateMembership(byCrate, allTracks));
    }

    /// <summary>Ids filed into the crate called <paramref name="name"/>, or empty when there is none.</summary>
    public IReadOnlyCollection<Guid> Members(string name)
    {
        var id = _names.FirstOrDefault(kv => kv.Value == name);
        if (id.Value is null) return Array.Empty<Guid>();
        return _members[id.Key];
    }
}

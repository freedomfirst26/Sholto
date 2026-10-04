using Sholto.App.Library;
using Sholto.App.Library.Catalog;

namespace Sholto.TestSupport;

/// <summary>A catalog that gives every track a fresh id the first time it is seen, and remembers it.</summary>
internal sealed class FakeTrackCatalog : ITrackCatalog
{
    private readonly Dictionary<string, Guid> _ids = [];

    public Task<TrackUpsertResult> UpsertAsync(IReadOnlyList<Track> tracks)
    {
        var added = new List<Guid>();
        foreach (var track in tracks)
        {
            if (_ids.ContainsKey(track.FilePath)) continue;
            var id = Guid.NewGuid();
            _ids[track.FilePath] = id;
            added.Add(id);
        }
        return Task.FromResult(new TrackUpsertResult(new Dictionary<string, Guid>(_ids), added));
    }

    public Guid IdOf(string filePath) => _ids[filePath];

    /// <summary>Give <paramref name="filePath"/> an id before any scan, so a test can key stored facts (tags)
    /// by it. A track assigned this way is already known, so a scan does not report it as new.</summary>
    public Guid Assign(string filePath)
    {
        if (_ids.TryGetValue(filePath, out var existing)) return existing;
        var id = Guid.NewGuid();
        _ids[filePath] = id;
        return id;
    }
}

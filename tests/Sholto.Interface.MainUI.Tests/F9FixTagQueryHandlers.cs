using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Answers the two tag queries the tag editor asks from in-memory lists: the tags a track already has,
/// and the known tag names (alphabetical) a prefix suggests from. Answers are already-completed tasks.</summary>
internal sealed class F9FixTagQueryHandlers :
    IQueryHandler<GetTrackTags, Task<IReadOnlyList<string>>>,
    IQueryHandler<SuggestTags, Task<IReadOnlyList<string>>>
{
    /// <summary>The tags each track has, as the database would hold them.</summary>
    public Dictionary<Guid, string[]> TagsByTrack { get; } = [];

    /// <summary>Every tag name the database knows, alphabetical.</summary>
    public List<string> Known { get; } = [];

    public Task<IReadOnlyList<string>> Handle(in GetTrackTags query) =>
        Task.FromResult<IReadOnlyList<string>>(TagsByTrack.TryGetValue(query.TrackId, out var tags) ? tags : Array.Empty<string>());

    public Task<IReadOnlyList<string>> Handle(in SuggestTags query)
    {
        var prefix = query.Prefix;
        var limit = query.Limit;
        return Task.FromResult<IReadOnlyList<string>>(
            Known.Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Take(limit).ToList());
    }
}

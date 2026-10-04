using Sholto.App.Library.Tags;

namespace Sholto.App.Tests;

/// <summary>An in-memory tag service that answers every call and records what it was asked. What an add
/// returns, and the hits the searches return, are set by the test.</summary>
internal sealed class F9TagService : ITagService
{
    public event EventHandler<TagsChangedEventArgs>? TagsChanged;

    public Dictionary<Guid, IReadOnlyList<string>> TagsByTrack { get; } = [];

    /// <summary>The track ids each tag name filters to, for <see cref="GetTrackIdsForTagAsync"/>.</summary>
    public Dictionary<string, IReadOnlyList<Guid>> TrackIdsByTag { get; } = [];

    public IReadOnlyList<string> Autocompletions { get; set; } = [];

    public IReadOnlyList<TagSearchHit> Hits { get; set; } = [];

    /// <summary>What an add answers; by default the raw text comes back as stored.</summary>
    public Func<string, AddTagResult> AddAnswer { get; set; } = raw => new AddTagResult(AddTagOutcome.Added, raw);

    public List<(Guid TrackId, string Raw)> Added { get; } = [];
    public List<(Guid TrackId, string Name)> Removed { get; } = [];
    public List<(string Prefix, int Limit)> AutocompleteCalls { get; } = [];
    public List<(string Query, int Limit)> SearchCalls { get; } = [];
    public List<int> TopCalls { get; } = [];
    public List<IReadOnlyCollection<string>> NameCalls { get; } = [];

    public void RaiseTagsChanged(Guid trackId, int newCount) =>
        TagsChanged?.Invoke(this, new TagsChangedEventArgs(trackId, newCount));

    public Task<IReadOnlyList<string>> GetTagsForTrackAsync(Guid trackId, CancellationToken ct) =>
        Task.FromResult(TagsByTrack.TryGetValue(trackId, out var tags) ? tags : Array.Empty<string>());

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetTagsByTrackAsync() =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(TagsByTrack);

    public Task<AddTagResult> AddTagAsync(Guid trackId, string raw, CancellationToken ct)
    {
        Added.Add((trackId, raw));
        return Task.FromResult(AddAnswer(raw));
    }

    public Task RemoveTagAsync(Guid trackId, string name, CancellationToken ct)
    {
        Removed.Add((trackId, name));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> AutocompleteAsync(string prefix, int limit, CancellationToken ct)
    {
        AutocompleteCalls.Add((prefix, limit));
        return Task.FromResult(Autocompletions);
    }

    public Task<IReadOnlyList<TagSearchHit>> TopTagsAsync(int limit, CancellationToken ct)
    {
        TopCalls.Add(limit);
        return Task.FromResult(Hits);
    }

    public Task<IReadOnlyList<TagSearchHit>> HitsForNamesAsync(IReadOnlyCollection<string> names, CancellationToken ct)
    {
        NameCalls.Add(names);
        return Task.FromResult(Hits);
    }

    public Task<IReadOnlyList<TagSearchHit>> SearchTagsAsync(string query, int limit, CancellationToken ct)
    {
        SearchCalls.Add((query, limit));
        return Task.FromResult(Hits);
    }

    public Task<IReadOnlyList<Guid>> GetTrackIdsForTagAsync(string name, CancellationToken ct) =>
        Task.FromResult(TrackIdsByTag.TryGetValue(name, out var ids) ? ids : Array.Empty<Guid>());
}

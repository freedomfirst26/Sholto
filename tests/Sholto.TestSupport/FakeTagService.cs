using Sholto.App.Library.Tags;

namespace Sholto.TestSupport;

/// <summary>Tags per track id, as the library session reads them. Only what a scan, a tag filter and a tag edit
/// notification use is real.</summary>
internal sealed class FakeTagService(IReadOnlyDictionary<Guid, IReadOnlyList<string>> tagsByTrack) : ITagService
{
    private readonly Dictionary<Guid, IReadOnlyList<string>> _tagsByTrack = new(tagsByTrack);

    public event EventHandler<TagsChangedEventArgs>? TagsChanged;

    /// <summary>Replace a track's tags and announce it, as an edit in the tag editor does.</summary>
    public void Edit(Guid trackId, params string[] tags)
    {
        _tagsByTrack[trackId] = tags;
        TagsChanged?.Invoke(this, new TagsChangedEventArgs(trackId, tags.Length));
    }

    public Task<IReadOnlyList<string>> GetTagsForTrackAsync(Guid trackId, CancellationToken ct) =>
        Task.FromResult(_tagsByTrack.TryGetValue(trackId, out var tags) ? tags : Array.Empty<string>());

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetTagsByTrackAsync() =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(_tagsByTrack);

    public Task<IReadOnlyList<Guid>> GetTrackIdsForTagAsync(string name, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            _tagsByTrack.Where(kv => kv.Value.Contains(name)).Select(kv => kv.Key).ToList());

    public Task<AddTagResult> AddTagAsync(Guid trackId, string raw, CancellationToken ct) => throw new NotSupportedException();

    public Task RemoveTagAsync(Guid trackId, string name, CancellationToken ct) => throw new NotSupportedException();

    public Task<IReadOnlyList<string>> AutocompleteAsync(string prefix, int limit, CancellationToken ct) => throw new NotSupportedException();

    public Task<IReadOnlyList<TagSearchHit>> TopTagsAsync(int limit, CancellationToken ct) => throw new NotSupportedException();

    public Task<IReadOnlyList<TagSearchHit>> HitsForNamesAsync(IReadOnlyCollection<string> names, CancellationToken ct) => throw new NotSupportedException();

    public Task<IReadOnlyList<TagSearchHit>> SearchTagsAsync(string query, int limit, CancellationToken ct) => throw new NotSupportedException();
}

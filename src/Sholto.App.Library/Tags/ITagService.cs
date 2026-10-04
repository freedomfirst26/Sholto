namespace Sholto.App.Library.Tags;

/// <summary>Tag mutations and lookups used by the tag editor and search.</summary>
public interface ITagService
{
    const int MaxTagsPerTrack = 128;

    Task<IReadOnlyList<string>> GetTagsForTrackAsync(Guid trackId, CancellationToken ct);

    /// <summary>Tag names (alphabetical) for every track that has at least one tag.
    /// Tracks with no tags are absent.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetTagsByTrackAsync();

    Task<AddTagResult> AddTagAsync(Guid trackId, string raw, CancellationToken ct);

    Task RemoveTagAsync(Guid trackId, string name, CancellationToken ct);

    Task<IReadOnlyList<string>> AutocompleteAsync(string prefix, int limit, CancellationToken ct);

    Task<IReadOnlyList<TagSearchHit>> TopTagsAsync(int limit, CancellationToken ct);

    Task<IReadOnlyList<TagSearchHit>> HitsForNamesAsync(IReadOnlyCollection<string> names, CancellationToken ct);

    Task<IReadOnlyList<TagSearchHit>> SearchTagsAsync(string query, int limit, CancellationToken ct);

    Task<IReadOnlyList<Guid>> GetTrackIdsForTagAsync(string name, CancellationToken ct);

    event EventHandler<TagsChangedEventArgs>? TagsChanged;
}

using Sholto.App.Library.Crates;
using Sholto.App.Library.Tags;
using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>Answers the library queries an interface asks: a track's tags, tag suggestions and search,
/// the most-used tags, tags by name, crate search. Each reads the database services of the library
/// session and answers an empty list while there is no database. Answers are tasks: the database reads
/// are async.</summary>
public sealed class LibraryQueryHandlers(ILibrarySession library) :
    IQueryHandler<GetTrackTags, Task<IReadOnlyList<string>>>,
    IQueryHandler<SuggestTags, Task<IReadOnlyList<string>>>,
    IQueryHandler<SearchTags, Task<IReadOnlyList<TagHit>>>,
    IQueryHandler<TopTags, Task<IReadOnlyList<TagHit>>>,
    IQueryHandler<TagsByName, Task<IReadOnlyList<TagHit>>>,
    IQueryHandler<SearchCrates, Task<IReadOnlyList<CrateRef>>>
{
    private readonly ILibrarySession _library = library;

    public Task<IReadOnlyList<string>> Handle(in GetTrackTags query) =>
        _library.Tags is { } tags
            ? tags.GetTagsForTrackAsync(query.TrackId, default)
            : Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> Handle(in SuggestTags query) =>
        _library.Tags is { } tags
            ? tags.AutocompleteAsync(query.Prefix, query.Limit, default)
            : Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<TagHit>> Handle(in SearchTags query) =>
        _library.Tags is { } tags ? HitsAsync(tags.SearchTagsAsync(query.Query, query.Limit, default)) : NoHits();

    public Task<IReadOnlyList<TagHit>> Handle(in TopTags query) =>
        _library.Tags is { } tags ? HitsAsync(tags.TopTagsAsync(query.Limit, default)) : NoHits();

    public Task<IReadOnlyList<TagHit>> Handle(in TagsByName query) =>
        _library.Tags is { } tags ? HitsAsync(tags.HitsForNamesAsync(query.Names, default)) : NoHits();

    public Task<IReadOnlyList<CrateRef>> Handle(in SearchCrates query) =>
        _library.Crates is { } crates ? CratesAsync(crates.SearchAsync(query.Query)) : Task.FromResult<IReadOnlyList<CrateRef>>([]);

    private Task<IReadOnlyList<TagHit>> NoHits() => Task.FromResult<IReadOnlyList<TagHit>>([]);

    private async Task<IReadOnlyList<TagHit>> HitsAsync(Task<IReadOnlyList<TagSearchHit>> hits) =>
        (await hits).Select(h => new TagHit(h.Name, h.TrackCount)).ToList();

    private async Task<IReadOnlyList<CrateRef>> CratesAsync(Task<IReadOnlyList<CrateSummary>> crates) =>
        (await crates).Select(c => new CrateRef(c.Id, c.Name, c.TrackCount)).ToList();
}

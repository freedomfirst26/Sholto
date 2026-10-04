using Sholto.App.Library.Tags;
using Sholto.Data;
using Sholto.App.Library;

namespace Sholto.App.Tests;

/// <summary>The library queries against a real library session: with the tag and crate services attached
/// each query returns the services' answer mapped to the Data types, with none attached each returns an
/// empty list, and every one is answerable through a real bus with <c>AskAsync</c>.</summary>
public class LibraryQueryHandlersTests
{
    private readonly LibrarySessionRig _rig = new();
    private readonly F9TagService _tags = new();
    private readonly LibraryQueryHandlers _handlers;
    private readonly Guid _trackId = Guid.NewGuid();

    public LibraryQueryHandlersTests()
    {
        _handlers = new LibraryQueryHandlers(_rig.Library);
    }

    private void Attach() => _rig.Library.AttachServices(_tags, _rig.Crates);

    private DataBus NewBus()
    {
        var bus = new DataBus(new ThrowingFailureSink());
        bus.Register<GetTrackTags, Task<IReadOnlyList<string>>>(_handlers);
        bus.Register<SuggestTags, Task<IReadOnlyList<string>>>(_handlers);
        bus.Register<SearchTags, Task<IReadOnlyList<TagHit>>>(_handlers);
        bus.Register<TopTags, Task<IReadOnlyList<TagHit>>>(_handlers);
        bus.Register<TagsByName, Task<IReadOnlyList<TagHit>>>(_handlers);
        bus.Register<SearchCrates, Task<IReadOnlyList<CrateRef>>>(_handlers);
        return bus;
    }

    // ---- With the services attached -------------------------------------------------------------

    [Fact]
    public async Task GetTrackTags_returns_the_tracks_tags()
    {
        _tags.TagsByTrack[_trackId] = ["peak", "vocal"];
        Attach();

        var result = await _handlers.Handle(new GetTrackTags(_trackId));

        Assert.Equal(["peak", "vocal"], result);
    }

    [Fact]
    public async Task GetTrackTags_for_an_untagged_track_is_empty()
    {
        Attach();

        var result = await _handlers.Handle(new GetTrackTags(_trackId));

        Assert.Empty(result);
    }

    [Fact]
    public async Task SuggestTags_returns_the_completions_for_the_prefix_and_limit()
    {
        _tags.Autocompletions = ["deep house", "deep techno"];
        Attach();

        var result = await _handlers.Handle(new SuggestTags("deep", 5));

        Assert.Equal(["deep house", "deep techno"], result);
        Assert.Equal([("deep", 5)], _tags.AutocompleteCalls);
    }

    [Fact]
    public async Task SearchTags_maps_the_hits_and_passes_the_query_and_limit()
    {
        _tags.Hits = [new TagSearchHit("house", 3), new TagSearchHit("house vocal", 1)];
        Attach();

        var result = await _handlers.Handle(new SearchTags("hou", 10));

        Assert.Equal([new TagHit("house", 3), new TagHit("house vocal", 1)], result);
        Assert.Equal([("hou", 10)], _tags.SearchCalls);
    }

    [Fact]
    public async Task TopTags_maps_the_hits_and_passes_the_limit()
    {
        _tags.Hits = [new TagSearchHit("techno", 12)];
        Attach();

        var result = await _handlers.Handle(new TopTags(7));

        Assert.Equal([new TagHit("techno", 12)], result);
        Assert.Equal([7], _tags.TopCalls);
    }

    [Fact]
    public async Task TagsByName_maps_the_hits_and_passes_the_names()
    {
        _tags.Hits = [new TagSearchHit("peak", 4)];
        Attach();
        var names = new[] { "peak", "warm" };

        var result = await _handlers.Handle(new TagsByName(names));

        Assert.Equal([new TagHit("peak", 4)], result);
        Assert.Same(names, Assert.Single(_tags.NameCalls));
    }

    [Fact]
    public async Task SearchCrates_maps_the_crates_to_refs_with_their_track_counts()
    {
        Attach();
        var crateId = await _rig.Crates.CreateAsync("Warmup");
        await _rig.Crates.AddTrackAsync(crateId, _trackId);

        var result = await _handlers.Handle(new SearchCrates("warm"));

        Assert.Equal([new CrateRef(crateId, "Warmup", 1)], result);
    }

    // ---- With no services attached --------------------------------------------------------------

    [Fact]
    public async Task Every_tag_query_is_empty_when_no_tag_service_is_attached()
    {
        Assert.Empty(await _handlers.Handle(new GetTrackTags(_trackId)));
        Assert.Empty(await _handlers.Handle(new SuggestTags("a", 5)));
        Assert.Empty(await _handlers.Handle(new SearchTags("a", 5)));
        Assert.Empty(await _handlers.Handle(new TopTags(5)));
        Assert.Empty(await _handlers.Handle(new TagsByName(["a"])));
    }

    [Fact]
    public async Task SearchCrates_is_empty_when_no_crate_service_is_attached()
    {
        Assert.Empty(await _handlers.Handle(new SearchCrates("a")));
    }

    // ---- Through a real bus ---------------------------------------------------------------------

    [Fact]
    public async Task All_six_queries_are_answerable_through_AskAsync_on_a_bus()
    {
        _tags.TagsByTrack[_trackId] = ["peak"];
        _tags.Autocompletions = ["peak time"];
        _tags.Hits = [new TagSearchHit("peak", 2)];
        Attach();
        var crateId = await _rig.Crates.CreateAsync("Warmup");
        var bus = NewBus();

        Assert.Equal(["peak"], await bus.AskAsync<GetTrackTags, IReadOnlyList<string>>(new GetTrackTags(_trackId)));
        Assert.Equal(["peak time"], await bus.AskAsync<SuggestTags, IReadOnlyList<string>>(new SuggestTags("pe", 5)));
        Assert.Equal([new TagHit("peak", 2)], await bus.AskAsync<SearchTags, IReadOnlyList<TagHit>>(new SearchTags("pe", 5)));
        Assert.Equal([new TagHit("peak", 2)], await bus.AskAsync<TopTags, IReadOnlyList<TagHit>>(new TopTags(5)));
        Assert.Equal([new TagHit("peak", 2)], await bus.AskAsync<TagsByName, IReadOnlyList<TagHit>>(new TagsByName(["peak"])));
        Assert.Equal([new CrateRef(crateId, "Warmup", 0)], await bus.AskAsync<SearchCrates, IReadOnlyList<CrateRef>>(new SearchCrates("w")));
    }

    [Fact]
    public async Task A_query_with_no_services_attached_is_empty_through_the_bus_too()
    {
        var bus = NewBus();

        Assert.Empty(await bus.AskAsync<TopTags, IReadOnlyList<TagHit>>(new TopTags(5)));
        Assert.Empty(await bus.AskAsync<SearchCrates, IReadOnlyList<CrateRef>>(new SearchCrates("")));
    }

    [Fact]
    public async Task A_query_nobody_handles_comes_back_as_a_completed_null_not_a_null_task()
    {
        var bus = new DataBus(new ThrowingFailureSink());

        var result = await bus.AskAsync<TopTags, IReadOnlyList<TagHit>>(new TopTags(5));

        Assert.Null(result);
    }
}

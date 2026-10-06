using Sholto.App.Analysis.Harmony;
using Sholto.App.Decks;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The search pick: while the Glance overlay is active, load, re-analyse and the browse knob act on the
/// pick, not the library selection; per-deck load generation; <see cref="LoadAccepted"/>.</summary>
public class SearchPickTests
{
    private static readonly Origin From = TrackLoaderRig.Origin;

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    private static SetSearchPick Pick(bool active, string? path) => new(active, path, From);

    /// <summary>Filters the library to Alpha only (so Bravo and Charlie are outside the visible rows) and
    /// highlights Alpha.</summary>
    private static async Task FilterToAlphaAsync(TrackLoaderRig rig)
    {
        var lib = rig.Library;
        var alphaId = lib.Catalog.Assign(LibrarySessionRig.Alpha.FilePath);
        var tags = new FakeTagService(new Dictionary<Guid, IReadOnlyList<string>>
        {
            [alphaId] = new[] { "house" },
        });
        await Task.Run(() => lib.Library.ScanAsync("/music", lib.Stack(tags: tags)));
        lib.Library.AttachServices(tags, lib.Crates);
        await lib.Library.FilterByTagAsync("house");
        Assert.Single(lib.Library.Rows);
        lib.Library.Select(0);
    }

    [Fact]
    public async Task A_pick_outside_the_visible_rows_loads_instead_of_the_library_selection()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        await FilterToAlphaAsync(rig);
        Assert.Equal(LibrarySessionRig.Alpha.FilePath, rig.Library.Library.SelectedTrack!.FilePath);

        rig.Pick.Handle(Pick(true, LibrarySessionRig.Charlie.FilePath));
        rig.Loader.Handle(new LoadSelectedIntoDeck(0, From));

        Assert.Equal(LibrarySessionRig.Charlie, rig.Library.Deck1.LoadedTrack);
        await Eventually(() => rig.Library.Deck1.LoadState == DeckLoadState.Loaded);
        Assert.Equal(new[] { LibrarySessionRig.Charlie.FilePath }, rig.Decoder.Decoded);
    }

    [Fact]
    public void An_active_pick_of_nothing_loads_nothing_and_publishes_no_LoadAccepted()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        rig.Highlight(LibrarySessionRig.Alpha);
        var accepted = new CountingEventHandler<LoadAccepted>();
        rig.Library.Bus.Subscribe(accepted);

        rig.Pick.Handle(Pick(true, null));
        rig.Loader.Handle(new LoadSelectedIntoDeck(0, From));

        Assert.Equal(DeckLoadState.Idle, rig.Library.Deck1.LoadState);
        Assert.Null(rig.Library.Deck1.LoadedTrack);
        Assert.Equal(0, accepted.Count);
        Assert.Empty(rig.Decoder.Decoded);
    }

    [Fact]
    public async Task An_inactive_pick_leaves_load_on_the_library_selection()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        rig.Highlight(LibrarySessionRig.Alpha);
        rig.Pick.Handle(Pick(true, LibrarySessionRig.Charlie.FilePath));

        rig.Pick.Handle(Pick(false, LibrarySessionRig.Charlie.FilePath));
        rig.Loader.Handle(new LoadSelectedIntoDeck(0, From));

        Assert.Equal(LibrarySessionRig.Alpha, rig.Library.Deck1.LoadedTrack);
        await Eventually(() => rig.Library.Deck1.LoadState == DeckLoadState.Loaded);
    }

    [Fact]
    public async Task Reanalysis_with_the_pick_active_lands_on_the_picked_file()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder(), new FakeAnalysisProvider(bpm: 126.5));
        rig.Highlight(LibrarySessionRig.Alpha);
        rig.Pick.Handle(Pick(true, LibrarySessionRig.Bravo.FilePath));

        rig.Loader.Handle(new ReanalyzeSelected(From));

        await Eventually(() => rig.Library.Row(LibrarySessionRig.Bravo).Bpm == 126.5);
        Assert.Null(rig.Library.Row(LibrarySessionRig.Alpha).Bpm);
        Assert.Equal(new[] { LibrarySessionRig.Bravo.FilePath }, rig.Decoder.Decoded);
    }

    [Fact]
    public void Rotate_with_the_pick_active_publishes_SearchCursorMoved_and_leaves_the_selection()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        rig.Highlight(LibrarySessionRig.Alpha);
        var before = rig.Library.Library.SelectedIndex;
        var moved = new CountingEventHandler<SearchCursorMoved>();
        rig.Library.Bus.Subscribe(moved);
        var browse = new BrowseCommandHandlers(rig.Library.Library, rig.Pick, rig.Library.Bus);
        rig.Pick.Handle(Pick(true, LibrarySessionRig.Bravo.FilePath));

        browse.Handle(new RotateBrowse(2, From));

        Assert.Equal(before, rig.Library.Library.SelectedIndex);
        Assert.Equal(1, moved.Count);
        Assert.Equal(new SearchCursorMoved(2), moved.Last);
    }

    [Fact]
    public void Rotate_with_the_pick_inactive_moves_the_selection_and_publishes_nothing()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        rig.Highlight(LibrarySessionRig.Alpha);
        var before = rig.Library.Library.SelectedIndex;
        var moved = new CountingEventHandler<SearchCursorMoved>();
        rig.Library.Bus.Subscribe(moved);
        var browse = new BrowseCommandHandlers(rig.Library.Library, rig.Pick, rig.Library.Bus);

        browse.Handle(new RotateBrowse(-before, From));
        browse.Handle(new RotateBrowse(2, From));

        Assert.Equal(2, rig.Library.Library.SelectedIndex);
        Assert.Equal(0, moved.Count);
    }

    [Fact]
    public void OpenSearch_publishes_exactly_one_SearchRequested_with_the_same_origin()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        var requested = new CountingEventHandler<SearchRequested>();
        rig.Library.Bus.Subscribe(requested);
        var browse = new BrowseCommandHandlers(rig.Library.Library, rig.Pick, rig.Library.Bus);

        browse.Handle(new OpenSearch(From));

        Assert.Equal(1, requested.Count);
        Assert.Equal(new SearchRequested(From), requested.Last);
    }

    [Fact]
    public void In_inspect_an_OpenSearch_from_the_controller_is_echoed_and_not_executed()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        var bus = rig.Library.Bus;
        var inspect = new InspectMode(bus);
        var registry = new InspectGatedCommandRegistry(bus, inspect, bus, [InterfaceIds.Controller, InterfaceIds.Keyboard]);
        registry.Register<SetInspectMode>(inspect);
        registry.Register<OpenSearch>(new BrowseCommandHandlers(rig.Library.Library, rig.Pick, bus));
        var requested = new CountingEventHandler<SearchRequested>();
        var echoed = new CountingEventHandler<CommandReceived>();
        bus.Subscribe(requested);
        bus.Subscribe(echoed);
        var origin = new Origin(InterfaceIds.Controller, "browse.push", "browse.press.short");

        bus.Send(new SetInspectMode(true, new Origin(InterfaceIds.Faceplate, "faceplate", "mount")));
        bus.Send(new OpenSearch(origin));

        Assert.Equal(0, requested.Count);
        Assert.Equal(1, echoed.Count);
        Assert.Equal("OpenSearch", echoed.Last.CommandName);
    }

    [Fact]
    public async Task When_the_first_loads_decode_finishes_last_the_deck_still_holds_the_second_load()
    {
        var decoder = new GatedAudioFileDecoder();
        var rig = new TrackLoaderRig(decoder);
        var a = LibrarySessionRig.Alpha;
        var b = LibrarySessionRig.Bravo;
        rig.Highlight(a);
        rig.Loader.Handle(new LoadSelectedIntoDeck(0, From));
        rig.Highlight(b);
        rig.Loader.Handle(new LoadSelectedIntoDeck(0, From));

        decoder.Release(b.FilePath);
        await Eventually(() => rig.Library.Deck1.LoadState == DeckLoadState.Loaded);
        decoder.Release(a.FilePath);
        await Task.Delay(300);   // let A's decode land on the app thread

        Assert.Equal(b, rig.Library.Deck1.LoadedTrack);
        Assert.Equal(DeckLoadState.Loaded, rig.Library.Deck1.LoadState);
        Assert.False(rig.Library.Row(a).IsPlayed);
    }

    [Fact]
    public async Task Each_accepted_load_publishes_LoadAccepted_once_and_raises_Accepted()
    {
        var rig = new TrackLoaderRig(new FakeAudioFileDecoder());
        var accepted = new CountingEventHandler<LoadAccepted>();
        rig.Library.Bus.Subscribe(accepted);
        var raised = new List<(int Deck, Track Track)>();
        rig.Loader.Accepted += (deck, track) => raised.Add((deck, track));
        rig.Highlight(LibrarySessionRig.Bravo);

        rig.Loader.Handle(new LoadSelectedIntoDeck(1, From));
        await Eventually(() => rig.Library.Deck2.LoadState == DeckLoadState.Loaded);

        Assert.Equal(1, accepted.Count);
        Assert.Equal(new LoadAccepted(1, "/music/b.mp3", "Bravo", "Abe"), accepted.Last);
        Assert.Equal((1, LibrarySessionRig.Bravo), Assert.Single(raised));
    }
}

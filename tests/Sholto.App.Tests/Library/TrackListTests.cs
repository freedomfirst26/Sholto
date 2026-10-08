using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The Track List over a real library session: what each load appends, de-duplication and source
/// counts, removal, moving, clearing, and how the shown rows (and the highlight) follow.</summary>
public class TrackListTests
{
    private static readonly Origin From = new(InterfaceIds.Bench, "test", "track-list");
    private static readonly string A = LibrarySessionRig.Alpha.FilePath;
    private static readonly string B = LibrarySessionRig.Bravo.FilePath;
    private static readonly string C = LibrarySessionRig.Charlie.FilePath;

    private readonly LibrarySessionRig _rig = new();
    private readonly RecordingHandler<TrackListChanged> _announced = new();
    private ITrackList _list = null!;
    private int _warmup;

    /// <summary>Catalog order is Bravo, Charlie, Alpha. Crate Warmup holds [Bravo, Alpha]; tag peak is Alpha and Charlie.</summary>
    private async Task<ITrackList> ReadyAsync()
    {
        var alpha = _rig.Catalog.Assign(A);
        var bravo = _rig.Catalog.Assign(B);
        var charlie = _rig.Catalog.Assign(C);
        var tags = new FakeTagService(new Dictionary<Guid, IReadOnlyList<string>>
        {
            [alpha] = new[] { "Peak" },
            [charlie] = new[] { "Peak" },
        });
        await _rig.Library.ScanAsync("/music", _rig.Stack(tags: tags));
        _rig.Library.AttachServices(tags, _rig.Crates);
        _warmup = await _rig.Crates.CreateAsync("Warmup");
        await _rig.Crates.AddTrackAsync(_warmup, bravo);
        await _rig.Crates.AddTrackAsync(_warmup, alpha);
        _rig.Bus.Subscribe(_announced);
        _list = new TrackList(_rig.Library, new ImmediateAppThread(), _rig.Bus);
        return _list;
    }

    private string[] Rows => _rig.Library.Rows.Select(r => r.FilePath).ToArray();

    private int[] Counts => _announced.Received[^1].Sources.Select(s => s.Count).ToArray();

    [Fact]
    public async Task Before_any_list_is_loaded_the_rows_are_the_catalog()
    {
        await ReadyAsync();

        Assert.Equal([B, C, A], Rows);
        Assert.Empty(_announced.Received[^1].Sources);
    }

    [Fact]
    public async Task The_announcement_carries_the_paths_in_list_order()
    {
        var list = await ReadyAsync();
        Assert.Empty(_announced.Received[^1].Paths);

        list.Handle(new LoadCrateToTrackList(_warmup, "Warmup", From));
        list.Handle(new LoadSongToTrackList(C, From));
        list.Handle(new MoveInTrackList(C, 0, From));

        Assert.Equal([C, B, A], _announced.Received[^1].Paths);
    }

    [Fact]
    public async Task Loading_a_crate_shows_its_songs_in_crate_order()
    {
        var list = await ReadyAsync();

        list.Handle(new LoadCrateToTrackList(_warmup, "Warmup", From));

        Assert.Equal([B, A], Rows);
    }

    [Fact]
    public async Task A_tag_load_appends_without_duplicates_and_each_source_counts_its_songs()
    {
        var list = await ReadyAsync();
        list.Handle(new LoadCrateToTrackList(_warmup, "Warmup", From));

        list.Handle(new LoadTagToTrackList("Peak", From));

        Assert.Equal([B, A, C], Rows);
        Assert.Equal([2, 2], Counts);
        Assert.Equal([$"crate:{_warmup}", "tag:peak"], _announced.Received[^1].Sources.Select(s => s.Key));
        Assert.Equal(3, _announced.Received[^1].Count);
    }

    [Fact]
    public async Task Loading_a_song_that_is_already_in_the_list_changes_nothing()
    {
        var list = await ReadyAsync();
        list.Handle(new LoadCrateToTrackList(_warmup, "Warmup", From));
        var announced = _announced.Received.Count;
        var changed = 0;
        list.Changed += () => changed++;

        list.Handle(new LoadSongToTrackList(A, From));

        Assert.Equal([B, A], Rows);
        Assert.Equal(announced, _announced.Received.Count);
        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task A_single_song_is_its_own_source()
    {
        var list = await ReadyAsync();

        list.Handle(new LoadSongToTrackList(C, From));

        Assert.Equal([C], Rows);
        var source = Assert.Single(_announced.Received[^1].Sources);
        Assert.Equal(("songs", TrackListSourceKind.Songs, 1), (source.Key, source.Kind, source.Count));
    }

    [Fact]
    public async Task Removing_a_source_removes_only_the_songs_it_alone_brought()
    {
        var list = await ReadyAsync();
        list.Handle(new LoadCrateToTrackList(_warmup, "Warmup", From));
        list.Handle(new LoadTagToTrackList("Peak", From));

        list.Handle(new RemoveSourceFromTrackList($"crate:{_warmup}", From));

        Assert.Equal([A, C], Rows);
        Assert.Equal([2], Counts);
    }

    [Fact]
    public async Task Removing_a_song_takes_it_out_of_the_list_and_the_counts()
    {
        var list = await ReadyAsync();
        list.Handle(new LoadTagToTrackList("Peak", From));

        list.Handle(new RemoveFromTrackList(C, From));

        Assert.Equal([A], Rows);
        Assert.Equal([1], Counts);
    }

    [Fact]
    public async Task Move_reorders_and_keeps_the_highlight_on_its_path()
    {
        var list = await ReadyAsync();
        list.Handle(new LoadCrateToTrackList(_warmup, "Warmup", From));
        list.Handle(new LoadTagToTrackList("Peak", From));
        _rig.Library.Select(1);
        Assert.Equal(A, _rig.Library.SelectedSummary!.FilePath);

        list.Handle(new MoveInTrackList(A, 2, From));

        Assert.Equal([B, C, A], Rows);
        Assert.Equal(A, _rig.Library.SelectedSummary!.FilePath);
        Assert.Equal(2, _rig.Library.SelectedIndex);
    }

    [Fact]
    public async Task Removing_the_highlighted_song_moves_the_highlight_to_the_next_row()
    {
        var list = await ReadyAsync();
        list.Handle(new LoadCrateToTrackList(_warmup, "Warmup", From));
        list.Handle(new LoadTagToTrackList("Peak", From));
        _rig.Library.Select(1);

        list.Handle(new RemoveFromTrackList(A, From));

        Assert.Equal([B, C], Rows);
        Assert.Equal(C, _rig.Library.SelectedSummary!.FilePath);
    }

    [Fact]
    public async Task Clear_empties_the_rows_and_announces_no_sources()
    {
        var list = await ReadyAsync();
        list.Handle(new LoadTagToTrackList("Peak", From));

        list.Handle(new ClearTrackList(From));

        Assert.Empty(Rows);
        Assert.Empty(_announced.Received[^1].Sources);
        Assert.Equal(0, _announced.Received[^1].Count);
    }

    [Fact]
    public async Task A_rescan_keeps_the_list()
    {
        var list = await ReadyAsync();
        list.Handle(new LoadCrateToTrackList(_warmup, "Warmup", From));

        await _rig.Library.ScanAsync("/music", _rig.Stack());

        Assert.Equal([B, A], Rows);
    }

    [Fact]
    public async Task Every_change_raises_Changed_but_a_restore_does_not()
    {
        var list = await ReadyAsync();
        var changed = 0;
        list.Changed += () => changed++;

        list.Handle(new LoadSongToTrackList(A, From));
        list.Handle(new MoveInTrackList(A, 0, From));
        Assert.Equal(1, changed);
        list.Handle(new LoadSongToTrackList(C, From));
        Assert.Equal(2, changed);

        list.Restore([new TrackListEntry(B, ["tag:x"])], [new TrackListSource("tag:x", TrackListSourceKind.Tag, "x", 1)]);

        Assert.Equal(2, changed);
        Assert.Equal([B, A, C], Rows);
    }

    [Fact]
    public async Task A_restore_puts_the_saved_songs_first_and_merges_a_song_loaded_before_it()
    {
        var list = await ReadyAsync();
        list.Handle(new LoadSongToTrackList(A, From));

        list.Restore(
            [new TrackListEntry(B, ["crate:7"]), new TrackListEntry(A, ["crate:7"])],
            [new TrackListSource("crate:7", TrackListSourceKind.Crate, "Seven", 2)]);

        Assert.Equal([B, A], Rows);
        Assert.Equal(["crate:7", "songs"], _announced.Received[^1].Sources.Select(s => s.Key));
        Assert.Equal([2, 1], Counts);
    }
}

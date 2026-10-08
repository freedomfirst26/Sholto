using Sholto.Data;

namespace Sholto.Data.Tests;

public class TrackListContractTests
{
    private static Origin Here => new("test", "ctl", "press");

    public static TheoryData<ICommand> Commands => new()
    {
        new LoadSongToTrackList("a.mp3", Here),
        new LoadCrateToTrackList(3, "Crate", Here),
        new LoadTagToTrackList("house", Here),
        new RemoveFromTrackList("a.mp3", Here),
        new RemoveSourceFromTrackList("crate:3", Here),
        new MoveInTrackList("a.mp3", 2, Here),
        new ClearTrackList(Here),
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void Every_track_list_command_carries_a_deckless_origin(ICommand command)
    {
        Assert.Equal(Origin.NoDeck, command.Origin.Deck);
        Assert.Equal(Here, command.Origin);
    }

    [Fact]
    public void Default_TrackListChanged_has_empty_sources()
    {
        var changed = default(TrackListChanged);
        Assert.NotNull(changed.Sources);
        Assert.Empty(changed.Sources);
        Assert.NotNull(changed.Paths);
        Assert.Empty(changed.Paths);
        Assert.Equal(0, changed.Slot);
    }

    [Fact]
    public void TrackListChanged_keeps_the_sources_it_was_given()
    {
        var source = new TrackListSource("songs", TrackListSourceKind.Songs, "Songs", 2);
        var changed = new TrackListChanged([source], 2);
        Assert.Same(source, Assert.Single(changed.Sources));
        Assert.Equal(2, changed.Count);
    }
}

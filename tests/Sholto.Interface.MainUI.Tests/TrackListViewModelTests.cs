using Sholto.Interface.MainUI.Controls.Chips;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The Track List strip: chips from the announced sources, the count and empty state, the remove and
/// move commands. Only commands go out; the list itself lives in the App.</summary>
public class TrackListViewModelTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly TrackListViewModel _vm;

    public TrackListViewModelTests() => _vm = new TrackListViewModel(_bus, _bus);

    private F9RecordingCommandHandler<T> Record<T>() where T : struct, ICommand
    {
        var handler = new F9RecordingCommandHandler<T>();
        _bus.Register<T>(handler);
        return handler;
    }

    private void Announce(int count, params TrackListSource[] sources) =>
        _bus.Publish(new TrackListChanged(sources, count));

    [Fact]
    public void A_new_view_model_is_empty()
    {
        Assert.True(_vm.IsEmpty);
        Assert.Empty(_vm.Sources);
    }

    [Fact]
    public void Two_sources_give_two_chips_with_their_names_and_counts()
    {
        Announce(70,
            new TrackListSource("crate:1", TrackListSourceKind.Crate, "Featurecast", 48),
            new TrackListSource("tag:industrial", TrackListSourceKind.Tag, "industrial", 22));

        Assert.Equal(2, _vm.Sources.Count);
        Assert.Equal("Featurecast", _vm.Sources[0].Label);
        Assert.Equal(SourceChipKind.Crate, _vm.Sources[0].ChipKind);
        Assert.Equal(48, _vm.Sources[0].Count);
        Assert.Equal("industrial", _vm.Sources[1].Label);
        Assert.Equal(SourceChipKind.Tag, _vm.Sources[1].ChipKind);
        Assert.Equal(22, _vm.Sources[1].Count);
        Assert.Equal(70, _vm.Count);
        Assert.Equal("70 songs", _vm.CountText);
        Assert.False(_vm.IsEmpty);
    }

    [Fact]
    public void One_source_shows_no_chip_count_because_the_total_repeats_it()
    {
        Announce(10, new TrackListSource("tag:vocal", TrackListSourceKind.Tag, "#Vocal", 10));

        Assert.False(_vm.Sources[0].ShowCount);
        Assert.Equal("10 songs", _vm.CountText);
    }

    [Fact]
    public void Two_sources_each_show_their_chip_count()
    {
        Announce(19,
            new TrackListSource("tag:peak", TrackListSourceKind.Tag, "Peak time", 9),
            new TrackListSource("tag:vocal", TrackListSourceKind.Tag, "Vocal", 10));

        Assert.All(_vm.Sources, c => Assert.True(c.ShowCount));
    }

    [Fact]
    public void Going_back_down_to_one_source_hides_its_chip_count_again()
    {
        Announce(19,
            new TrackListSource("tag:peak", TrackListSourceKind.Tag, "Peak time", 9),
            new TrackListSource("tag:vocal", TrackListSourceKind.Tag, "Vocal", 10));
        Announce(10, new TrackListSource("tag:vocal", TrackListSourceKind.Tag, "Vocal", 10));

        Assert.False(_vm.Sources[0].ShowCount);
    }

    [Fact]
    public void Loose_songs_read_as_a_song_count()
    {
        Announce(1, new TrackListSource("songs", TrackListSourceKind.Songs, "Songs", 1));

        Assert.Equal("♪ 1 song", _vm.Sources[0].Label);
        Assert.Equal("1 song", _vm.CountText);
    }

    [Fact]
    public void A_chip_cross_sends_exactly_one_RemoveSourceFromTrackList_with_its_key()
    {
        var removed = Record<RemoveSourceFromTrackList>();
        Announce(70,
            new TrackListSource("crate:1", TrackListSourceKind.Crate, "Featurecast", 48),
            new TrackListSource("tag:industrial", TrackListSourceKind.Tag, "industrial", 22));

        _vm.RemoveSource(_vm.Sources[1].Key);

        var command = Assert.Single(removed.Received);
        Assert.Equal("tag:industrial", command.SourceKey);
        Assert.Equal(InterfaceIds.MainUI, command.Origin.InterfaceId);
    }

    [Fact]
    public void Clearing_the_list_shows_the_empty_state()
    {
        Announce(3, new TrackListSource("songs", TrackListSourceKind.Songs, "Songs", 3));
        Announce(0);

        Assert.True(_vm.IsEmpty);
        Assert.Empty(_vm.Sources);
    }

    [Fact]
    public void RemoveSong_with_no_path_sends_nothing()
    {
        var removed = Record<RemoveFromTrackList>();

        _vm.RemoveSong(null);

        Assert.Empty(removed.Received);
    }

    [Theory]
    [InlineData(0, 3, 2)]   // grab row 0, drop in the gap below row 2 -> lands at index 2
    [InlineData(3, 0, 0)]   // grab row 3, drop above the first row
    [InlineData(1, 5, 4)]   // below the last of five rows
    public void Dropping_in_a_gap_sends_MoveInTrackList_to_the_landing_index(int from, int gap, int expected)
    {
        var moved = Record<MoveInTrackList>();
        _vm.BeginDrag("/music/x.mp3", from, 5);
        Assert.True(_vm.IsDragging);

        _vm.UpdateDrag(gap);

        Assert.Equal(gap, _vm.DropGap);
        Assert.True(_vm.CompleteDrag());
        var command = Assert.Single(moved.Received);
        Assert.Equal("/music/x.mp3", command.Path);
        Assert.Equal(expected, command.ToIndex);
        Assert.False(_vm.IsDragging);
    }

    [Theory]
    [InlineData(2, 2)]   // the gap just above its own row
    [InlineData(2, 3)]   // the gap just below its own row
    public void Dropping_a_row_where_it_already_is_sends_nothing(int from, int gap)
    {
        var moved = Record<MoveInTrackList>();
        _vm.BeginDrag("/music/x.mp3", from, 5);
        _vm.UpdateDrag(gap);

        Assert.False(_vm.CompleteDrag());

        Assert.Empty(moved.Received);
    }

    [Fact]
    public void A_cancelled_drag_sends_nothing_and_a_gap_is_clamped_to_the_rows()
    {
        var moved = Record<MoveInTrackList>();
        _vm.BeginDrag("/music/x.mp3", 0, 3);
        _vm.UpdateDrag(99);
        Assert.Equal(3, _vm.DropGap);

        _vm.CancelDrag();

        Assert.False(_vm.CompleteDrag());
        Assert.Empty(moved.Received);
    }
}

using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Dragging a song in the Track List, end to end: the view model's drag over the real App Track List
/// and library session, with no window in between.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class TrackListDragEndToEndTests
{
    [Fact]
    public async Task Dragging_the_first_song_below_the_last_sends_one_move_and_the_App_reorders_the_rows()
    {
        var rig = await TrackListDragRig.CreateAsync();
        Assert.Equal([TrackListDragRig.A, TrackListDragRig.B, TrackListDragRig.C], rig.RowPaths);

        rig.Vm.TrackList.BeginDrag(TrackListDragRig.A, 0, rig.Vm.Tracks.Count);
        rig.Vm.TrackList.UpdateDrag(3);
        Assert.True(rig.Vm.TrackList.CompleteDrag());

        var move = Assert.IsType<MoveInTrackList>(Assert.Single(rig.Sender.Sent));
        Assert.Equal(TrackListDragRig.A, move.Path);
        Assert.Equal(2, move.ToIndex);
        Assert.Equal([TrackListDragRig.B, TrackListDragRig.C, TrackListDragRig.A], rig.ListPaths);
        Assert.Equal([TrackListDragRig.B, TrackListDragRig.C, TrackListDragRig.A], rig.RowPaths);
    }
}

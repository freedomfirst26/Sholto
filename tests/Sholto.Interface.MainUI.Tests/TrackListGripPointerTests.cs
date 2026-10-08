using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The grip on a Track List row, driven by real pointer input through a real window: press on the grip,
/// move down, release.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class TrackListGripPointerTests
{
    [Fact]
    public async Task Dragging_the_grip_of_the_first_row_below_the_last_row_reorders_the_list()
    {
        var rig = await TrackListDragRig.CreateAsync();
        var window = rig.Window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var list = window.FindControl<ListBox>("TrackList")!;
            var first = (Control)list.ContainerFromIndex(0)!;
            var last = (Control)list.ContainerFromIndex(2)!;
            // The grip is the 22 px column at the left edge of the row.
            var press = first.TranslatePoint(new Point(11, first.Bounds.Height / 2), window)!.Value;
            var below = last.TranslatePoint(new Point(11, last.Bounds.Height * 0.9), window)!.Value;

            window.MouseMove(press);
            window.MouseDown(press, MouseButton.Left);
            window.MouseMove(new Point(press.X, (press.Y + below.Y) / 2));
            window.MouseMove(below);
            window.MouseUp(below, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var move = Assert.IsType<Sholto.Data.MoveInTrackList>(Assert.Single(rig.Sender.Sent));
            Assert.Equal(TrackListDragRig.A, move.Path);
            Assert.Equal(2, move.ToIndex);
            Assert.Equal([TrackListDragRig.B, TrackListDragRig.C, TrackListDragRig.A], rig.ListPaths);
            Assert.Equal([TrackListDragRig.B, TrackListDragRig.C, TrackListDragRig.A], rig.RowPaths);
        }
        finally { window.Close(); }
    }
}

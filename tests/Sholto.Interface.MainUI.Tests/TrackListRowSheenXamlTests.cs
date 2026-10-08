using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Sholto.Interface.MainUI.Controls;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The pointer sheen on Track List rows, driven by real pointer input through a real window.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class TrackListRowSheenXamlTests
{
    private static RowSheen SheenOf(ListBox list, int index) =>
        ((Control)list.ContainerFromIndex(index)!).GetVisualDescendants().OfType<RowSheen>().Single();

    /// <summary>Lets the 140 ms fade run its course.</summary>
    private static void Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Thread.Sleep(25);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static Point InRow(ListBox list, Window window, int index, double x)
    {
        var row = (Control)list.ContainerFromIndex(index)!;
        return row.TranslatePoint(new Point(x, row.Bounds.Height / 2), window)!.Value;
    }

    [Fact]
    public async Task Every_realised_row_has_exactly_one_sheen_spanning_the_row()
    {
        var rig = await TrackListDragRig.CreateAsync();
        var window = rig.Window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var list = window.FindControl<ListBox>("TrackList")!;
            for (var i = 0; i < 3; i++)
            {
                var row = (Control)list.ContainerFromIndex(i)!;
                var sheen = SheenOf(list, i);
                Assert.Equal(row.Bounds.Width, sheen.Bounds.Width, 1);
                Assert.Equal(row.Bounds.Height, sheen.Bounds.Height, 1);
                Assert.False(sheen.IsHitTestVisible);
            }
        }
        finally { window.Close(); }
    }

    [Fact]
    public async Task The_sheen_of_the_hovered_row_follows_the_pointer_and_the_other_rows_stay_dark()
    {
        var rig = await TrackListDragRig.CreateAsync();
        var window = rig.Window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var list = window.FindControl<ListBox>("TrackList")!;
            window.MouseMove(InRow(list, window, 0, 200));
            Settle();
            Assert.Equal(200, SheenOf(list, 0).PointerX, 2);
            Assert.True(SheenOf(list, 0).Intensity > 0);

            window.MouseMove(InRow(list, window, 0, 400));
            Settle();
            Assert.Equal(400, SheenOf(list, 0).PointerX, 2);
            Assert.True(SheenOf(list, 0).Intensity > 0);

            Assert.Equal(0, SheenOf(list, 1).Intensity);
            Assert.Equal(0, SheenOf(list, 2).Intensity);
        }
        finally { window.Close(); }
    }

    [Fact]
    public async Task Hovering_changes_no_row_or_cell_bounds()
    {
        var rig = await TrackListDragRig.CreateAsync();
        var window = rig.Window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var list = window.FindControl<ListBox>("TrackList")!;
            List<Rect> Bounds() => Enumerable.Range(0, 3)
                .SelectMany(i => ((Control)list.ContainerFromIndex(i)!).GetVisualDescendants().OfType<Control>()
                    .Prepend((Control)list.ContainerFromIndex(i)!))
                .Select(c => c.Bounds).ToList();

            var before = Bounds();
            window.MouseMove(InRow(list, window, 1, 300));
            Settle();
            window.MouseMove(InRow(list, window, 2, 500));
            Settle();
            Assert.Equal(before, Bounds());
        }
        finally { window.Close(); }
    }

    [Fact]
    public async Task The_sheen_fades_out_when_the_pointer_leaves_the_list()
    {
        var rig = await TrackListDragRig.CreateAsync();
        var window = rig.Window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var list = window.FindControl<ListBox>("TrackList")!;
            window.MouseMove(InRow(list, window, 0, 300));
            Settle();
            Assert.Equal(1, SheenOf(list, 0).Intensity, 3);

            window.MouseMove(new Point(5, 5));
            Settle();
            Assert.Equal(0, SheenOf(list, 0).Intensity, 3);
        }
        finally { window.Close(); }
    }

    [Fact]
    public async Task Under_reduced_motion_the_sheen_rests_on_the_title_column_wherever_the_pointer_is()
    {
        var rig = await TrackListDragRig.CreateAsync(reducedMotion: true);
        var window = rig.Window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var list = window.FindControl<ListBox>("TrackList")!;
            window.MouseMove(InRow(list, window, 0, 200));
            Dispatcher.UIThread.RunJobs();
            var sheen = SheenOf(list, 0);
            var grid = (Grid)sheen.GetVisualParent()!;
            var expected = grid.ColumnDefinitions.Take(4).Sum(c => c.ActualWidth) + grid.ColumnDefinitions[4].ActualWidth / 2;
            Assert.Equal(expected, sheen.PointerX, 2);
            // No fade: the full value is there at once.
            Assert.True(sheen.Intensity > 0.99);

            window.MouseMove(InRow(list, window, 0, 600));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(expected, sheen.PointerX, 2);
        }
        finally { window.Close(); }
    }
}

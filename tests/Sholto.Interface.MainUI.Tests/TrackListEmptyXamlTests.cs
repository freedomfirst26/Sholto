using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The empty Track List state: a large slanted plate that opens search, and the main window hiding the S
/// watermark, the strip and the column headers while it shows.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class TrackListEmptyXamlTests
{
    public TrackListEmptyXamlTests() => AvaloniaTestApp.EnsureStarted();

    private static readonly TrackListSource[] OneSource = [new("songs", TrackListSourceKind.Songs, "Songs", 2)];

    [Fact]
    public void The_empty_state_shows_while_empty_and_hides_once_songs_are_loaded()
    {
        var bus = new DataBus(new ThrowingFailureSink());
        var empty = new TrackListEmpty { DataContext = new TrackListViewModel(bus, bus) };
        var state = empty.FindControl<Panel>("EmptyState")!;
        Assert.True(state.IsVisible);

        bus.Publish(new TrackListChanged(OneSource, 2));

        Assert.False(state.IsVisible);
    }

    [Fact]
    public void The_keycaps_are_Space_and_Ctrl_L()
    {
        var empty = new TrackListEmpty();

        var keys = empty.GetLogicalDescendants().OfType<Border>().Where(b => b.Classes.Contains("keycap"))
            .Select(b => ((TextBlock)b.Child!).Text).ToArray();
        Assert.Equal(["Space", "Ctrl L"], keys);
        Assert.Contains("Track List is empty",
            empty.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
    }

    [Fact]
    public void Clicking_the_plate_raises_SearchRequested_once()
    {
        var empty = new TrackListEmpty();
        var raised = 0;
        empty.SearchRequested += (_, _) => raised++;

        empty.FindControl<Button>("OpenSearchPlate")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Clicking_the_plate_on_the_main_window_opens_search()
    {
        var rig = await TrackListDragRig.CreateAsync();
        Clear(rig);
        var window = rig.Window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.False(rig.Vm.IsSearchOpen);

            window.FindControl<TrackListEmpty>("TrackListEmptyState")!
                .FindControl<Button>("OpenSearchPlate")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.True(rig.Vm.IsSearchOpen);
        }
        finally { window.Close(); }
    }

    private static void Clear(TrackListDragRig rig) =>
        rig.List.Handle(new ClearTrackList(new Origin(InterfaceIds.MainUI, "test", "clear")));

    [Fact]
    public async Task A_main_list_with_rows_never_shows_the_plate_even_when_the_Track_List_count_is_0()
    {
        var rig = await TrackListDragRig.CreateAsync();
        var window = rig.Window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.NotEmpty(rig.Vm.Tracks);

            // The Track List reports 0 songs while the rows are still on screen (before the first restore).
            rig.Library.Bus.Publish(new TrackListChanged([], 0));
            Dispatcher.UIThread.RunJobs();

            Assert.True(rig.Vm.TrackList.IsEmpty);
            Assert.False(window.FindControl<TrackListEmpty>("TrackListEmptyState")!.IsVisible);
            Assert.True(window.FindControl<Control>("SholtoWatermark")!.IsVisible);
            Assert.True(window.FindControl<Control>("TrackListStrip")!.IsVisible);
            Assert.True(window.FindControl<Control>("ColumnHeaders")!.IsVisible);
        }
        finally { window.Close(); }
    }

    [Fact]
    public async Task The_S_watermark_strip_and_headers_show_only_once_the_list_has_songs()
    {
        var rig = await TrackListDragRig.CreateAsync();
        var bus = rig.Library.Bus;
        var window = rig.Window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var plate = window.FindControl<TrackListEmpty>("TrackListEmptyState")!;
            var watermark = window.FindControl<Control>("SholtoWatermark")!;
            var strip = window.FindControl<Control>("TrackListStrip")!;
            var headers = window.FindControl<Control>("ColumnHeaders")!;

            // Loaded (the rig holds three songs): the S, strip and headers are back, the plate is gone.
            Assert.False(plate.IsVisible);
            Assert.True(watermark.IsVisible);
            Assert.True(strip.IsVisible);
            Assert.True(headers.IsVisible);

            // Clear: the plate returns and the S, strip and headers hide.
            Clear(rig);
            Dispatcher.UIThread.RunJobs();
            Assert.True(plate.IsVisible);
            Assert.False(watermark.IsVisible);
            Assert.False(strip.IsVisible);
            Assert.False(headers.IsVisible);

            // Songs again: everything flips back.
            bus.Publish(new TrackListChanged(OneSource, 2));
            Dispatcher.UIThread.RunJobs();
            Assert.False(plate.IsVisible);
            Assert.True(watermark.IsVisible);
        }
        finally { window.Close(); }
    }
}

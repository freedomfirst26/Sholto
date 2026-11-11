using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>What the overlay does with keys, list clicks and the view model's motion cues, read off the view model
/// and the commands the rig logs.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class SearchOverlayBehaviourTests
{
    public SearchOverlayBehaviourTests() => AvaloniaTestApp.EnsureStarted();

    private static SearchOverlayRig Open(bool database = false, Action<GlanceRig>? beforeOpen = null)
    {
        var rig = new SearchOverlayRig();
        rig.Rig.Show(GlanceRig.Track("Alpha"), GlanceRig.Track("Bravo"), GlanceRig.Track("Charlie"));
        if (database) rig.Rig.AttachDatabase();
        beforeOpen?.Invoke(rig.Rig);
        rig.Rig.Glance.Open();
        Dispatcher.UIThread.RunJobs();
        rig.Overlay.UpdateLayout();
        return rig;
    }

    private static KeyEventArgs Press(TextBox box, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };
        box.RaiseEvent(args);
        return args;
    }

    private static void Type(TextBox box, string text) =>
        box.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text });

    [Fact]
    public void Down_moves_the_table_highlight_and_is_handled()
    {
        var rig = Open();
        var box = rig.Overlay.FindControl<TextBox>("QueryBox")!;
        var list = rig.Overlay.FindControl<ListBox>("ResultsList")!;
        Assert.Equal(0, rig.Rig.Glance.TableIndex);

        var args = Press(box, Key.Down);

        Assert.True(args.Handled);
        Assert.Equal(1, rig.Rig.Glance.TableIndex);
        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void Shift_1_loads_to_deck_one_and_swallows_the_typed_character()
    {
        var rig = Open();
        var box = rig.Overlay.FindControl<TextBox>("QueryBox")!;

        var args = Press(box, Key.D1, KeyModifiers.Shift);
        Type(box, "!");
        Type(box, "x");

        Assert.True(args.Handled);
        Assert.Equal(0, Assert.Single(rig.Rig.Log.OfType<LoadSelectedIntoDeck>()).Deck);
        Assert.Equal("x", box.Text);
    }

    [Fact]
    public void Ctrl_L_adds_the_highlighted_song_to_the_track_list()
    {
        var rig = Open();
        var box = rig.Overlay.FindControl<TextBox>("QueryBox")!;

        var args = Press(box, Key.L, KeyModifiers.Control);

        Assert.True(args.Handled);
        Assert.Single(rig.Rig.Log.OfType<LoadSongToTrackList>());
    }

    [Fact]
    public void Escape_closes_the_overlay()
    {
        var rig = Open();
        var box = rig.Overlay.FindControl<TextBox>("QueryBox")!;
        Assert.True(rig.Rig.Glance.IsOpen);

        Press(box, Key.Escape);

        Assert.False(rig.Rig.Glance.IsOpen);
    }

    [Fact]
    public void Clicking_a_table_row_moves_the_highlight_there()
    {
        var rig = Open();
        var list = rig.Overlay.FindControl<ListBox>("ResultsList")!;
        rig.Rig.Glance.ToggleZone();
        Assert.Equal(GlanceZone.Rail, rig.Rig.Glance.Zone);

        list.SelectedIndex = 2;

        Assert.Equal(2, rig.Rig.Glance.TableIndex);
        Assert.Equal(GlanceZone.Table, rig.Rig.Glance.Zone);
    }

    [Fact]
    public void Clicking_a_rail_header_puts_the_rail_selection_back()
    {
        var rig = Open(database: true, g =>
        {
            g.Crates.Add(new CrateRef(1, "Warm", 3));
            g.Crates.Add(new CrateRef(2, "Peak", 4));
        });
        var rail = rig.Overlay.FindControl<ListBox>("RailList")!;
        Assert.Equal(1, rig.Rig.Glance.RailIndex);

        rail.SelectedIndex = 0;

        Assert.Equal(1, rail.SelectedIndex);
    }

    [Fact]
    public void A_new_result_marks_the_table_fresh()
    {
        var rig = Open();
        var list = rig.Overlay.FindControl<ListBox>("ResultsList")!;

        Assert.Contains("fresh", list.Classes);
    }

    [Fact]
    public void A_song_joining_the_track_list_flashes_its_row()
    {
        var rig = Open();
        var list = rig.Overlay.FindControl<ListBox>("ResultsList")!;

        rig.Rig.PutInList(GlanceRig.Track("Alpha") with { FilePath = "/music/Alpha.mp3" });

        var row = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0));
        Assert.Contains("flash", row.Classes);
    }

    [Fact]
    public void A_crate_whose_count_changed_ticks()
    {
        var rig = Open(database: true, g => g.Crates.Add(new CrateRef(1, "Warm", 3)));
        rig.Rig.Glance.Close();
        rig.Rig.Crates[0] = new CrateRef(1, "Warm", 5);
        rig.Rig.Glance.Open();
        Dispatcher.UIThread.RunJobs();
        rig.Overlay.UpdateLayout();
        var rail = rig.Overlay.FindControl<ListBox>("RailList")!;

        var index = rig.Rig.Glance.RailItems.ToList().FindIndex(i => i is GlanceRailCrate);
        var row = Assert.IsType<ListBoxItem>(rail.ContainerFromIndex(index));
        Assert.Contains("tick", row.Classes);
    }
}

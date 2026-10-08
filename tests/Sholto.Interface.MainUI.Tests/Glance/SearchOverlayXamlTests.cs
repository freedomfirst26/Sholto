using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls;
using Sholto.Interface.MainUI.ViewModels.Glance;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The Glance overlay as a control: built from its own XAML, with the tag-completion ghost and the Tab hint present.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class SearchOverlayXamlTests
{
    public SearchOverlayXamlTests() => AvaloniaTestApp.EnsureStarted();

    [Fact]
    public void The_overlay_builds_with_its_completion_ghost_and_tab_hint()
    {
        var overlay = new SearchOverlay();
        Assert.NotNull(overlay.FindControl<TextBlock>("CompletionGhost"));
        Assert.NotNull(overlay.FindControl<TextBlock>("TabHintText"));
    }

    [Fact]
    public void The_overlay_has_no_load_keys_no_track_list_slot_and_no_FIT_text()
    {
        var overlay = new SearchOverlay();
        foreach (var name in new[] { "LoadDeck1Key", "LoadDeck2Key", "LoadTrackListKey", "TrackListSlot" })
            Assert.Null(overlay.FindControl<Control>(name));
        var texts = overlay.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.DoesNotContain("FIT", texts);
        Assert.DoesNotContain("LOAD TO", texts);
        Assert.DoesNotContain("Undo", texts);
    }

    [Fact]
    public void The_footer_shows_the_track_list_count()
    {
        var rig = new SearchOverlayRig();
        var count = rig.Overlay.FindControl<TextBlock>("TrackListCountText");
        Assert.NotNull(count);
        Assert.Equal("0", count.Text);

        rig.Rig.Bus.Publish(new TrackListChanged([new TrackListSource("songs", TrackListSourceKind.Songs, "Songs", 7)], 7));

        Assert.Equal("7", count.Text);
    }

    [Fact]
    public void Clicking_a_deck_slot_loads_the_highlighted_song_there()
    {
        var rig = new SearchOverlayRig();
        rig.Rig.Show(GlanceRig.Track("Alpha"), GlanceRig.Track("Bravo"));
        rig.Rig.Glance.Open();
        rig.Overlay.UpdateLayout();

        var slots = rig.Overlay.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("slot")).ToList();
        Assert.Equal(2, slots.Count);
        Assert.True(slots[0].Classes.Contains("d1") && !slots[0].Classes.Contains("d2"));
        Assert.True(slots[1].Classes.Contains("d2") && !slots[1].Classes.Contains("d1"));
        slots[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var load = Assert.Single(rig.Rig.Log.OfType<LoadSelectedIntoDeck>());
        Assert.Equal(1, load.Deck);
    }

    [Fact]
    public void The_status_line_has_no_Q_or_Shortlist_hint()
    {
        var overlay = new SearchOverlay();
        var texts = overlay.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.DoesNotContain("Q", texts);
        Assert.DoesNotContain("Shortlist", texts);
    }

    private static readonly IBrush Key8A = new SolidColorBrush(Colors.Red);

    /// <summary>Deck 1 loaded and paused (the target), deck 2 loaded and playing (the other deck), overlay laid out.</summary>
    private static (SearchOverlayRig Rig, List<Button> Slots) OpenWithTwoLoadedDecks()
    {
        var rig = new SearchOverlayRig();
        rig.Rig.Decks.Set(0, new DeckClockReading(true, false, "Static Bloom", 360, 80, 1.0, "9A", Key8A, 124));
        rig.Rig.Decks.Set(1, new DeckClockReading(true, true, "Pressure Systems", 360, 80, 1.0, "8A", Key8A, 128));
        rig.Rig.SuggestedTarget = 0;
        rig.Rig.Show(GlanceRig.Track("A"));
        rig.Rig.Answer = q => Task.FromResult(
            GlanceRig.Ranked(q.TargetDeck, [GlanceRig.Track("A")], fitActive: true) with { ReferenceDeck = 1 });
        rig.Rig.Glance.Open();
        rig.Overlay.UpdateLayout();
        var slots = rig.Overlay.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("slot")).ToList();
        return (rig, slots);
    }

    [Fact]
    public void A_slot_is_64_tall_and_its_platter_52_square()
    {
        var (_, slots) = OpenWithTwoLoadedDecks();
        foreach (var slot in slots)
        {
            Assert.Equal(64, slot.Bounds.Height);
            var platter = slot.GetVisualDescendants().OfType<DeckSlotPlatter>().Single(p => !p.Ghost);
            Assert.Equal(52, platter.Bounds.Width);
            Assert.Equal(52, platter.Bounds.Height);
        }
    }

    [Fact]
    public void The_targets_stats_are_at_full_opacity()
    {
        var (_, slots) = OpenWithTwoLoadedDecks();
        var stats = slots[0].GetVisualDescendants().OfType<StackPanel>().Single(p => p.Classes.Contains("stats"));
        Assert.Equal(1.0, stats.Opacity);
    }

    [Fact]
    public void The_slot_has_no_slanted_key_and_its_platter_label_reads_the_shift_shortcut()
    {
        var (_, slots) = OpenWithTwoLoadedDecks();
        foreach (var slot in slots)
            Assert.DoesNotContain(slot.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("loadkey"));
        foreach (var (slot, label) in slots.Zip(new[] { "⇧1", "⇧2" }))
        {
            var number = slot.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("platternumber"));
            Assert.Equal(label, number.Inlines is { Count: > 0 } ? string.Concat(number.Inlines.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)) : number.Text);
        }
        Assert.DoesNotContain(slots.SelectMany(s => s.GetVisualDescendants().OfType<TextBlock>()), t => t.Text == "LOADS HERE");
    }

    [Fact]
    public void Only_the_target_slot_is_washed_in_its_deck_colour()
    {
        var (rig, slots) = OpenWithTwoLoadedDecks();
        Assert.Same(rig.Overlay.FindResource("SholtoSlotDeck1Wash"), slots[0].Background);
        Assert.NotSame(rig.Overlay.FindResource("SholtoSlotDeck2Wash"), slots[1].Background);
        Assert.NotSame(rig.Overlay.FindResource("SholtoSlotDeck1Wash"), slots[1].Background);
    }

    [Fact]
    public void The_other_deck_carries_no_status_ok_border_or_shadow()
    {
        var (rig, slots) = OpenWithTwoLoadedDecks();
        Assert.True(rig.Rig.Header.Slots[1].IsReference);
        var ok = ((SolidColorBrush)rig.Overlay.FindResource("SholtoStatusOk")!).Color;
        bool IsOk(IBrush? b) => b is ISolidColorBrush s && s.Color == ok;

        Assert.False(IsOk(slots[1].BorderBrush));
        foreach (var border in slots[1].GetVisualDescendants().OfType<Border>())
        {
            Assert.False(IsOk(border.BorderBrush));
            foreach (var shadow in border.BoxShadow) Assert.NotEqual(ok, shadow.Color);
        }
        foreach (var presenter in slots[1].GetVisualDescendants().OfType<ContentPresenter>())
        {
            Assert.False(IsOk(presenter.BorderBrush));
            foreach (var shadow in presenter.BoxShadow) Assert.NotEqual(ok, shadow.Color);
        }
    }

    [Fact]
    public void An_empty_slots_ghost_record_is_64_square()
    {
        var rig = new SearchOverlayRig();
        rig.Rig.SuggestedTarget = 0;
        rig.Rig.Show(GlanceRig.Track("A"));
        rig.Rig.Glance.Open();
        rig.Overlay.UpdateLayout();
        var ghost = rig.Overlay.GetVisualDescendants().OfType<DeckSlotPlatter>().First(p => p.Ghost);
        Assert.Equal(64, ghost.Bounds.Width);
        Assert.Equal(64, ghost.Bounds.Height);
    }
}

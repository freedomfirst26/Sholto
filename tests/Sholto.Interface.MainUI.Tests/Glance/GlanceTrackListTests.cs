using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>Search loads into three places: Deck 1, Deck 2 and the Track List. The keys' states follow the
/// highlight, Ctrl L adds to the list and leaves the overlay open, and clearing takes two presses.</summary>
public class GlanceTrackListTests
{
    private readonly GlanceRig _rig = new();
    private GlanceViewModel Glance => _rig.Glance;

    private static readonly TrackSummary Alpha = GlanceRig.Track("Alpha");
    private static readonly TrackSummary Bravo = GlanceRig.Track("Bravo");
    private static readonly CrateRef Featurecast = new(7, "Featurecast", 48);
    private static readonly TagHit Industrial = new("Industrial", 22);

    private void OpenOnRail(object wanted)
    {
        _rig.Crates.Add(Featurecast);
        _rig.Tags.Add(Industrial);
        _rig.AttachDatabase();
        Glance.Open();
        Glance.ToggleZone();
        var target = Glance.RailItems.ToList().FindIndex(i => i switch
        {
            GlanceRailCrate c => wanted is CrateRef w && w.Id == c.Id,
            GlanceRailTag t => wanted is TagHit g && g.Name == t.Name,
            _ => false,
        });
        Assert.True(target >= 0);
        while (Glance.RailIndex < target) Glance.Move(1);
    }

    /// <summary>The table shows <paramref name="song"/>, which is not among the library's visible rows.</summary>
    private void OpenOnTableSong(TrackSummary song)
    {
        _rig.Show(Bravo);
        _rig.Answer = q => Task.FromResult(GlanceRig.Ranked(q.TargetDeck, [song]));
        Glance.Open();
    }

    private static TrackListChanged ListOf(string key, TrackListSourceKind kind, string name, int count) =>
        new([new TrackListSource(key, kind, name, count)], count);

    [Fact]
    public void A_rail_crate_offers_the_list_with_its_count_and_no_deck()
    {
        OpenOnRail(Featurecast);

        Assert.Equal(GlanceLoadState.Ready, Glance.TrackListLoadState);
        Assert.Equal(48, Glance.TrackListLoadCount);
        Assert.Equal(GlanceLoadState.Off, Glance.Deck1LoadState);
        Assert.Equal(GlanceLoadState.Off, Glance.Deck2LoadState);
    }

    [Fact]
    public void Loading_a_rail_crate_sends_one_command_and_the_overlay_stays_open()
    {
        OpenOnRail(Featurecast);

        Glance.LoadToTrackList();

        var sent = Assert.Single(_rig.Log.OfType<LoadCrateToTrackList>());
        Assert.Equal(7, sent.CrateId);
        Assert.Equal("Featurecast", sent.Name);
        Assert.True(Glance.IsOpen);
    }

    [Fact]
    public void Loading_a_rail_tag_sends_the_tag_and_marks_it_recently_used()
    {
        OpenOnRail(Industrial);

        Glance.LoadToTrackList();

        Assert.Equal("Industrial", Assert.Single(_rig.Log.OfType<LoadTagToTrackList>()).Tag);
        Assert.True(Glance.IsOpen);
    }

    [Fact]
    public void A_table_song_loads_to_the_list_without_touching_a_deck()
    {
        OpenOnTableSong(Alpha);

        Assert.Equal(GlanceLoadState.Ready, Glance.Deck1LoadState);
        Assert.Equal(GlanceLoadState.Ready, Glance.Deck2LoadState);
        Assert.Equal(GlanceLoadState.Ready, Glance.TrackListLoadState);
        Assert.Equal(0, Glance.TrackListLoadCount);

        Glance.LoadToTrackList();

        Assert.Equal(Alpha.FilePath, Assert.Single(_rig.Log.OfType<LoadSongToTrackList>()).Path);
        Assert.Empty(_rig.Log.OfType<LoadSelectedIntoDeck>());
        Assert.True(Glance.IsOpen);
    }

    [Fact]
    public void A_crate_already_in_the_list_is_marked_and_sends_nothing()
    {
        OpenOnRail(Featurecast);

        _rig.Bus.Publish(ListOf("crate:7", TrackListSourceKind.Crate, "Featurecast", 48));

        Assert.True(Glance.RailItems.OfType<GlanceRailCrate>().Single(c => c.Id == 7).IsInTrackList);
        Assert.Equal(GlanceLoadState.InList, Glance.TrackListLoadState);
        Glance.LoadToTrackList();
        Assert.Empty(_rig.Log.OfType<LoadCrateToTrackList>());
    }

    [Fact]
    public void A_tag_in_the_list_is_matched_by_its_lowercased_key()
    {
        OpenOnRail(Industrial);

        _rig.Bus.Publish(ListOf("tag:industrial", TrackListSourceKind.Tag, "Industrial", 22));

        Assert.True(Glance.RailItems.OfType<GlanceRailTag>().Single().IsInTrackList);
        Assert.Equal(GlanceLoadState.InList, Glance.TrackListLoadState);
    }

    [Fact]
    public void A_song_in_the_list_is_marked_and_sends_nothing()
    {
        _rig.Show(Alpha);
        _rig.PutInList(Alpha);
        Glance.Open();

        Assert.True(Glance.Rows.Single().IsInTrackList);
        Assert.Equal(GlanceLoadState.InList, Glance.TrackListLoadState);
        Glance.LoadToTrackList();
        Assert.Empty(_rig.Log.OfType<LoadSongToTrackList>());
    }

    [Fact]
    public void A_song_joining_the_list_raises_AddedToTrackList_once()
    {
        OpenOnTableSong(Alpha);
        var raised = new List<string>();
        Glance.AddedToTrackList += raised.Add;

        _rig.PutInList(Alpha);

        Assert.Equal([Alpha.FilePath], raised);
        Assert.True(Glance.Rows.Single().IsInTrackList);
    }

    [Fact]
    public void Library_rows_mirroring_the_catalog_mark_nothing_while_the_list_is_empty()
    {
        var charlie = GlanceRig.Track("Charlie");
        _rig.Show(Alpha, Bravo, charlie);
        Glance.Open();

        Assert.NotEmpty(Glance.Rows);
        Assert.All(Glance.Rows, r => Assert.False(r.IsInTrackList));
        Assert.Equal(0, Glance.TrackListCount);
    }

    [Fact]
    public void Only_the_songs_in_the_list_are_marked()
    {
        var charlie = GlanceRig.Track("Charlie");
        _rig.Show(Alpha, Bravo, charlie);
        Glance.Open();

        _rig.PutInList(Bravo);

        Assert.Equal([Bravo.FilePath], Glance.Rows.Where(r => r.IsInTrackList).Select(r => r.FilePath));
    }

    [Fact]
    public void Rows_changing_without_the_list_changing_raises_AddedToTrackList_for_nothing()
    {
        _rig.Show(Alpha, Bravo);
        Glance.Open();
        var raised = new List<string>();
        Glance.AddedToTrackList += raised.Add;

        _rig.Show(Alpha, Bravo, GlanceRig.Track("Charlie"));

        Assert.Empty(raised);
    }

    [Fact]
    public void AddedToTrackList_fires_for_the_song_that_joins_not_for_the_ones_already_in()
    {
        _rig.Show(Alpha, Bravo);
        _rig.PutInList(Alpha);
        Glance.Open();
        var raised = new List<string>();
        Glance.AddedToTrackList += raised.Add;

        _rig.PutInList(Alpha, Bravo);

        Assert.Equal([Bravo.FilePath], raised);
    }

    [Fact]
    public void A_song_already_in_the_list_raises_nothing()
    {
        _rig.Show(Alpha);
        _rig.PutInList(Alpha);
        Glance.Open();
        var raised = new List<string>();
        Glance.AddedToTrackList += raised.Add;

        _rig.PutInList(Alpha);

        Assert.Empty(raised);
    }

    [Fact]
    public void The_star_on_a_song_not_in_the_list_sends_one_LoadSongToTrackList()
    {
        OpenOnTableSong(Alpha);
        Assert.False(Glance.Rows.Single().IsInTrackList);

        Glance.ToggleHighlightedInTrackList();

        var sent = Assert.Single(_rig.Log.OfType<LoadSongToTrackList>());
        Assert.Equal(Alpha.FilePath, sent.Path);
        Assert.Empty(_rig.Log.OfType<RemoveFromTrackList>());
    }

    [Fact]
    public void The_star_on_a_song_in_the_list_sends_one_RemoveFromTrackList()
    {
        _rig.Show(Alpha);
        _rig.PutInList(Alpha);
        Glance.Open();
        Assert.True(Glance.Rows.Single().IsInTrackList);

        Glance.ToggleHighlightedInTrackList();

        var sent = Assert.Single(_rig.Log.OfType<RemoveFromTrackList>());
        Assert.Equal(Alpha.FilePath, sent.Path);
        Assert.Empty(_rig.Log.OfType<LoadSongToTrackList>());
    }

    [Fact]
    public void The_star_sends_nothing_while_the_overlay_is_closed()
    {
        _rig.Show(Alpha);

        Glance.ToggleHighlightedInTrackList();

        Assert.Empty(_rig.Log.OfType<LoadSongToTrackList>());
        Assert.Empty(_rig.Log.OfType<RemoveFromTrackList>());
    }

    [Fact]
    public void The_list_count_follows_TrackListChanged()
    {
        OpenOnRail(Featurecast);

        _rig.Bus.Publish(ListOf("crate:7", TrackListSourceKind.Crate, "Featurecast", 48));

        Assert.Equal(48, Glance.TrackListCount);
    }

    [Fact]
    public void Clearing_takes_two_presses_the_first_only_arms()
    {
        OpenOnRail(Featurecast);
        _rig.Bus.Publish(ListOf("crate:7", TrackListSourceKind.Crate, "Featurecast", 48));

        Glance.ClearTrackList();

        Assert.Empty(_rig.Log.OfType<ClearTrackList>());
        Assert.Equal("Ctrl Del again: clear 48", Glance.ClearArmedText);

        _rig.Clock.Advance(1.0);
        Glance.ClearTrackList();

        Assert.Single(_rig.Log.OfType<ClearTrackList>());
        Assert.Null(Glance.ClearArmedText);
    }

    [Fact]
    public void An_armed_clear_lapses_after_two_seconds()
    {
        OpenOnRail(Featurecast);
        _rig.Bus.Publish(ListOf("crate:7", TrackListSourceKind.Crate, "Featurecast", 48));
        Glance.ClearTrackList();

        _rig.Clock.Advance(2.5);
        _rig.Tick();
        Assert.Null(Glance.ClearArmedText);

        Glance.ClearTrackList();
        Assert.Empty(_rig.Log.OfType<ClearTrackList>());
    }
}

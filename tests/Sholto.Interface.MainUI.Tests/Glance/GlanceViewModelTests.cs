using System.ComponentModel;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The Glance view model on a real bus with fake App handlers: it asks the App what to rank and
/// where to load, shows the newest answer, tells the App which track the highlight points at, and acts on
/// the browse knob and the keys.</summary>
public class GlanceViewModelTests
{
    private readonly GlanceRig _rig = new();
    private GlanceViewModel Glance => _rig.Glance;

    private static readonly TrackSummary Alpha = GlanceRig.Track("Alpha");
    private static readonly TrackSummary Bravo = GlanceRig.Track("Bravo");
    private static readonly TrackSummary Charlie = GlanceRig.Track("Charlie");
    private static readonly TrackSummary Delta = GlanceRig.Track("Delta");

    private void ShowFour() => _rig.Show(Alpha, Bravo, Charlie, Delta);

    // ---- Open ----------------------------------------------------------------------------------

    [Fact]
    public void Opening_asks_the_App_for_the_target_and_ranks_for_it()
    {
        ShowFour();
        _rig.SuggestedTarget = 1;

        Glance.Open();

        Assert.True(Glance.IsOpen);
        Assert.Equal(1, Glance.Target);
        Assert.Equal(1, _rig.Ranker.Asked[0].TargetDeck);
    }

    [Fact]
    public void Typing_ranks_for_the_query_and_a_stale_answer_is_ignored()
    {
        ShowFour();
        var slow = new TaskCompletionSource<RankedTracks>();
        var fast = new TaskCompletionSource<RankedTracks>();
        var pending = new Queue<TaskCompletionSource<RankedTracks>>([slow, fast]);
        Glance.Open();
        _rig.Answer = _ => pending.Dequeue().Task;

        Glance.Query = "b";
        Glance.Query = "bsn";
        Assert.Equal("bsn", _rig.Ranker.Asked[^1].Query);

        fast.SetResult(GlanceRig.Ranked(0, [Charlie]));
        slow.SetResult(GlanceRig.Ranked(0, [Alpha, Bravo]));

        Assert.Equal(["Charlie"], Glance.Rows.Select(r => r.Row.Title));
    }

    [Fact]
    public void The_query_survives_a_close_and_is_selected_again_on_reopen()
    {
        ShowFour();
        var selected = 0;
        Glance.SelectAllOnOpen += () => selected++;
        Glance.Open();
        Glance.Query = "bsn";
        Glance.Close();
        selected = 0;

        Glance.Open();

        Assert.Equal("bsn", Glance.Query);
        Assert.Equal(1, selected);
        Assert.Equal("bsn", _rig.Ranker.Asked[^1].Query);
    }

    [Fact]
    public void Closing_tells_the_App_the_search_is_over()
    {
        ShowFour();
        Glance.Open();

        Glance.Close();

        var last = _rig.Picks.Last();
        Assert.False(last.Active);
        Assert.Null(last.FilePath);
    }

    // ---- Target and reference ------------------------------------------------------------------

    [Fact]
    public void The_reference_is_the_deck_that_is_not_the_target_and_flipping_re_ranks()
    {
        ShowFour();
        _rig.Answer = q => Task.FromResult(GlanceRig.Ranked(q.TargetDeck,
            q.TargetDeck == 0 ? [Alpha, Bravo] : [Bravo, Alpha]));
        Glance.Open();
        Assert.Equal("Alpha", Glance.Rows[0].Row.Title);
        Assert.Equal(1, _rig.Header.ReferenceDeck);

        Glance.FlipTarget();

        Assert.Equal(1, Glance.Target);
        Assert.Equal(1, _rig.Ranker.Asked[^1].TargetDeck);
        Assert.Equal(0, _rig.Header.ReferenceDeck);
        Assert.Equal("Bravo", Glance.Rows[0].Row.Title);
        var last = _rig.Picks.Last();
        Assert.True(last.Active);
        Assert.Equal(Bravo.FilePath, last.FilePath);
    }

    [Fact]
    public void A_pending_replace_warning_for_a_deck_retargets_the_list_to_it()
    {
        ShowFour();
        Glance.Open();
        Assert.Equal(0, Glance.Target);

        _rig.Bus.Publish(new LoadConfirmPending(true, 1, "In", "Out", 90));

        Assert.Equal(1, Glance.Target);
    }

    [Fact]
    public void Both_decks_empty_turns_ranking_off()
    {
        ShowFour();
        _rig.Answer = q => Task.FromResult(GlanceRig.Ranked(q.TargetDeck, [Alpha], fitActive: false));

        Glance.Open();

        Assert.False(_rig.Header.HasReference);
    }

    // ---- Knob and keys -------------------------------------------------------------------------

    [Fact]
    public void A_browse_push_opens_a_closed_overlay_and_then_toggles_the_zone()
    {
        ShowFour();
        var origin = new Origin(InterfaceIds.Controller, "browse", "press");

        _rig.Bus.Publish(new SearchRequested(origin));
        Assert.True(Glance.IsOpen);
        Assert.Equal(GlanceZone.Table, Glance.Zone);

        _rig.Bus.Publish(new SearchRequested(origin));
        Assert.Equal(GlanceZone.Rail, Glance.Zone);

        _rig.Bus.Publish(new SearchRequested(origin));
        Assert.Equal(GlanceZone.Table, Glance.Zone);
    }

    [Fact]
    public void The_knob_moves_the_highlight_clamps_at_the_end_and_tells_the_App()
    {
        ShowFour();
        Glance.Open();

        _rig.Bus.Publish(new SearchCursorMoved(3));
        Assert.Equal(3, Glance.TableIndex);
        Assert.Equal(Delta.FilePath, _rig.Picks.Last().FilePath);

        _rig.Bus.Publish(new SearchCursorMoved(3));
        Assert.Equal(3, Glance.TableIndex);
    }

    [Fact]
    public void A_pick_is_sent_only_when_it_changes()
    {
        ShowFour();
        Glance.Open();
        var before = _rig.Picks.Count();

        Glance.Move(0);
        Glance.Move(-1);

        Assert.Equal(before, _rig.Picks.Count());
    }

    [Fact]
    public void Q_toggles_the_shortlist_only_while_the_box_is_empty()
    {
        ShowFour();
        Glance.Open();

        Assert.True(Glance.TryShortlistKey());
        Assert.Equal(Alpha.FilePath, _rig.Log.OfType<ToggleShortlist>().Single().FilePath);

        Glance.Query = "bsn";
        _rig.Log.Clear();

        Assert.False(Glance.TryShortlistKey());
        Assert.Empty(_rig.Log.OfType<ToggleShortlist>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Shift_1_and_Shift_2_aim_at_that_deck_and_load_the_highlighted_track(int deck)
    {
        ShowFour();
        Glance.Open();
        Glance.Move(1);

        Glance.LoadTo(deck);

        Assert.Equal(deck, Glance.Target);
        var load = Assert.Single(_rig.Log.OfType<LoadSelectedIntoDeck>());
        Assert.Equal(deck, load.Deck);
        var pickIndex = _rig.Log.FindLastIndex(o => o is SetSearchPick { FilePath: var p } && p == Bravo.FilePath);
        Assert.True(pickIndex >= 0 && pickIndex < _rig.Log.IndexOf(load));
    }

    [Fact]
    public void Shift_1_loads_the_row_that_was_highlighted_even_when_it_flips_the_target()
    {
        ShowFour();
        Glance.Open();
        Glance.Move(1);

        Glance.LoadTo(1);

        Assert.Equal(Bravo.FilePath, _rig.Picks.Last().FilePath);
        Assert.IsType<LoadSelectedIntoDeck>(_rig.Log[^1]);
        // And the re-rank that follows keeps the highlight on that row, so a second press asks for the same track.
        Assert.Equal(Bravo.FilePath, Glance.HighlightedPath);
    }

    [Fact]
    public void Enter_on_a_track_picks_it_then_loads_it_to_the_target()
    {
        ShowFour();
        Glance.Open();
        Glance.Move(2);

        Glance.Activate();

        var load = _rig.Log.OfType<LoadSelectedIntoDeck>().Single();
        Assert.Equal(0, load.Deck);
        var pick = _rig.Log.FindLastIndex(o => o is SetSearchPick { FilePath: var p } && p == Charlie.FilePath);
        Assert.True(pick >= 0 && pick < _rig.Log.IndexOf(load));
    }

    [Fact]
    public void A_load_that_the_App_accepted_closes_the_overlay()
    {
        ShowFour();
        Glance.Open();

        _rig.Bus.Publish(new LoadAccepted(0, Alpha.FilePath, "Alpha", "Artist"));

        Assert.False(Glance.IsOpen);
    }

    // ---- Rail ----------------------------------------------------------------------------------

    private void FillRail()
    {
        _rig.Crates.Add(new CrateRef(7, "Peak time", 12));
        _rig.Tags.Add(new TagHit("techno", 40));
        _rig.AttachDatabase();
        _rig.Bus.Publish(new ShortlistChanged([Bravo]));
        _rig.Bus.Publish(new RecentLoadsChanged([Charlie, Delta]));
    }

    [Fact]
    public void The_rail_lists_shortlist_recent_loads_crates_and_tags_in_that_order()
    {
        ShowFour();
        FillRail();

        Glance.Open();

        Assert.Equal(
            ["SHORTLIST", "RECENT LOADS", "CRATES", "TAGS"],
            Glance.RailItems.OfType<GlanceRailHeader>().Select(h => h.Title));
        Assert.Equal([1, 2, 1, 1], Glance.RailItems.OfType<GlanceRailHeader>().Select(h => h.Count));
        Assert.IsType<GlanceRailHeader>(Glance.RailItems[0]);
        Assert.IsType<GlanceRailTrack>(Glance.RailItems[1]);
    }

    [Fact]
    public void The_rail_highlight_never_lands_on_a_header()
    {
        ShowFour();
        FillRail();
        Glance.Open();
        Glance.ToggleZone();
        Assert.IsNotType<GlanceRailHeader>(Glance.RailItems[Glance.RailIndex]);

        for (var i = 0; i < 12; i++)
        {
            Glance.Move(1);
            Assert.IsNotType<GlanceRailHeader>(Glance.RailItems[Glance.RailIndex]);
        }
        for (var i = 0; i < 12; i++)
        {
            Glance.Move(-1);
            Assert.IsNotType<GlanceRailHeader>(Glance.RailItems[Glance.RailIndex]);
        }
    }

    [Fact]
    public void A_rail_track_is_the_pick_and_a_crate_is_not()
    {
        ShowFour();
        FillRail();
        Glance.Open();
        Glance.ToggleZone();
        Assert.Equal(Bravo.FilePath, _rig.Picks.Last().FilePath);

        Glance.Move(10);

        Assert.IsType<GlanceRailTag>(Glance.RailItems[Glance.RailIndex]);
        Assert.Null(_rig.Picks.Last().FilePath);
        Assert.True(_rig.Picks.Last().Active);
    }

    [Fact]
    public void Ctrl_Enter_on_a_crate_filters_the_library_and_closes()
    {
        ShowFour();
        FillRail();
        Glance.Open();
        Glance.ToggleZone();
        while (Glance.RailItems[Glance.RailIndex] is not GlanceRailCrate) Glance.Move(1);

        Glance.ActivateAlternate();

        var filter = _rig.Log.OfType<FilterLibraryByCrate>().Single();
        Assert.Equal(7, filter.CrateId);
        Assert.Equal("Peak time", filter.Name);
        Assert.False(Glance.IsOpen);
    }

    [Fact]
    public void Ctrl_Enter_on_a_tag_filters_the_library_and_closes()
    {
        ShowFour();
        FillRail();
        Glance.Open();
        Glance.ToggleZone();
        while (Glance.RailItems[Glance.RailIndex] is not GlanceRailTag) Glance.Move(1);

        Glance.ActivateAlternate();

        Assert.Equal("techno", _rig.Log.OfType<FilterLibraryByTag>().Single().Tag);
        Assert.False(Glance.IsOpen);
    }

    [Fact]
    public void A_shortlisted_track_is_marked_in_the_table()
    {
        ShowFour();
        _rig.Bus.Publish(new ShortlistChanged([Bravo]));

        Glance.Open();

        Assert.Equal([false, true, false, false], Glance.Rows.Select(r => r.IsShortlisted));
        _rig.Bus.Publish(new ShortlistChanged([]));
        Assert.All(Glance.Rows, r => Assert.False(r.IsShortlisted));
    }

    // ---- Coalescing ----------------------------------------------------------------------------

    [Fact]
    public void Twenty_summary_changes_in_one_frame_cost_one_more_ranking()
    {
        ShowFour();
        Glance.Open();
        var before = _rig.Ranker.Asked.Count;

        for (var i = 0; i < 20; i++) _rig.Bus.Publish(new TrackSummaryChanged(Alpha with { Bpm = 100 + i }));
        Assert.Equal(before, _rig.Ranker.Asked.Count);

        _rig.Tick();
        Assert.Equal(before + 1, _rig.Ranker.Asked.Count);

        _rig.Tick();
        Assert.Equal(before + 1, _rig.Ranker.Asked.Count);
    }

    [Fact]
    public void A_ranking_still_in_flight_defers_the_next_one()
    {
        ShowFour();
        Glance.Open();
        var gate = new TaskCompletionSource<RankedTracks>();
        _rig.Answer = _ => gate.Task;
        _rig.Bus.Publish(new TrackSummaryChanged(Alpha));
        _rig.Tick();
        var asked = _rig.Ranker.Asked.Count;

        _rig.Bus.Publish(new TrackSummaryChanged(Bravo));
        _rig.Tick();
        Assert.Equal(asked, _rig.Ranker.Asked.Count);

        _rig.Answer = q => Task.FromResult(GlanceRig.Ranked(q.TargetDeck, [Alpha]));
        gate.SetResult(GlanceRig.Ranked(0, [Alpha]));
        _rig.Tick();
        Assert.Equal(asked + 1, _rig.Ranker.Asked.Count);
    }

    [Fact]
    public void A_closed_overlay_does_not_re_rank()
    {
        ShowFour();
        Glance.Open();
        Glance.Close();
        var before = _rig.Ranker.Asked.Count;

        _rig.Bus.Publish(new TrackSummaryChanged(Alpha));
        _rig.Tick();

        Assert.Equal(before, _rig.Ranker.Asked.Count);
    }

    [Fact]
    public void A_re_rank_keeps_the_highlight_on_the_same_track()
    {
        ShowFour();
        Glance.Open();
        Glance.Move(2);
        _rig.Answer = q => Task.FromResult(GlanceRig.Ranked(q.TargetDeck, [Charlie, Alpha, Bravo, Delta]));

        _rig.Bus.Publish(new TrackSummaryChanged(Alpha));
        _rig.Tick();

        Assert.Equal(0, Glance.TableIndex);
        Assert.Equal(Charlie.FilePath, Glance.HighlightedPath);
    }

    [Fact]
    public void Row_notifications_arrive_once_per_result_not_per_row()
    {
        ShowFour();
        var changes = new List<string?>();
        ((INotifyPropertyChanged)Glance).PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        Glance.Open();

        Assert.Single(changes, n => n == nameof(Glance.Rows));
    }
}

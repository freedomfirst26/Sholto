using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>Chips: crates and tags the person stacks in the search box, ANDed, from the rail.</summary>
public class GlanceChipTests
{
    private readonly GlanceRig _rig = new();
    private GlanceViewModel Glance => _rig.Glance;

    private static readonly TrackSummary Alpha = GlanceRig.Track("Alpha");
    private static readonly TrackSummary Bravo = GlanceRig.Track("Bravo");
    private static readonly CrateRef Peak = new(7, "Peak time", 12);
    private static readonly CrateRef Warm = new(8, "Warm up", 9);
    private static readonly TagHit Vocal = new("Vocal", 30);

    public GlanceChipTests()
    {
        _rig.Show(Alpha, Bravo);
        _rig.Crates.AddRange([Peak, Warm]);
        _rig.Tags.Add(Vocal);
        _rig.AttachDatabase();
        Glance.Open();
        Glance.ToggleZone();
    }

    private void Pick(object wanted)
    {
        Assert.Equal(GlanceZone.Rail, Glance.Zone);
        var target = Glance.RailItems.ToList().FindIndex(i => i switch
        {
            GlanceRailCrate c => wanted is CrateRef w && w.Id == c.Id,
            GlanceRailTag t => wanted is TagHit g && g.Name == t.Name,
            _ => false,
        });
        Assert.True(target >= 0);
        while (Glance.RailIndex < target) Glance.Move(1);
        while (Glance.RailIndex > target) Glance.Move(-1);
    }

    private GlanceRailCrate CrateItem(int id) => Glance.RailItems.OfType<GlanceRailCrate>().Single(c => c.Id == id);

    [Fact]
    public void Activating_a_crate_adds_a_chip_ranks_with_it_and_stays_open_without_filtering_the_library()
    {
        Pick(Peak);

        Glance.ActivateRailItem();

        Assert.Equal([new GlanceChip(GlanceChipKind.Crate, 7, "Peak time")], Glance.Chips);
        Assert.Equal([7], _rig.Ranker.Asked[^1].CrateIds);
        Assert.Empty(_rig.Ranker.Asked[^1].Tags);
        Assert.True(Glance.IsOpen);
        Assert.Empty(_rig.Log.OfType<FilterLibraryByCrate>());
        Assert.True(CrateItem(7).IsActive);
    }

    [Fact]
    public void Chips_stack_and_AND_and_activating_an_active_item_removes_only_that_chip()
    {
        Pick(Peak);
        Glance.ActivateRailItem();
        Pick(Vocal);
        Glance.ActivateRailItem();

        Assert.Equal([7], _rig.Ranker.Asked[^1].CrateIds);
        Assert.Equal(["Vocal"], _rig.Ranker.Asked[^1].Tags);
        Assert.Equal(2, Glance.Chips.Count);

        Pick(Peak);
        Glance.ActivateRailItem();

        Assert.Equal([new GlanceChip(GlanceChipKind.Tag, 0, "Vocal")], Glance.Chips);
        Assert.Empty(_rig.Ranker.Asked[^1].CrateIds);
        Assert.Equal(["Vocal"], _rig.Ranker.Asked[^1].Tags);
        Assert.Equal(GlanceZone.Rail, Glance.Zone);
    }

    [Fact]
    public void Enter_on_a_rail_crate_toggles_the_chip()
    {
        Pick(Peak);

        Glance.Activate();

        Assert.Single(Glance.Chips);
        Assert.True(Glance.IsOpen);
    }

    [Fact]
    public void Ctrl_Enter_filters_the_library_and_closes_and_leaves_the_chips_alone()
    {
        Pick(Warm);
        Glance.ActivateRailItem();
        Pick(Peak);

        Glance.ActivateAlternate();

        var filter = Assert.Single(_rig.Log.OfType<FilterLibraryByCrate>());
        Assert.Equal(7, filter.CrateId);
        Assert.False(Glance.IsOpen);
        Assert.Equal([new GlanceChip(GlanceChipKind.Crate, 8, "Warm up")], Glance.Chips);
    }

    [Fact]
    public void Backspace_on_an_empty_query_removes_the_last_chip_and_is_refused_without_one()
    {
        Pick(Peak);
        Glance.ActivateRailItem();
        Pick(Vocal);
        Glance.ActivateRailItem();

        Assert.True(Glance.RemoveLastChip());
        Assert.Equal([new GlanceChip(GlanceChipKind.Crate, 7, "Peak time")], Glance.Chips);
        Assert.Empty(_rig.Ranker.Asked[^1].Tags);

        Assert.True(Glance.RemoveLastChip());
        Assert.False(Glance.RemoveLastChip());
    }

    [Fact]
    public void Backspace_with_text_in_the_box_leaves_the_chips_to_the_text()
    {
        Pick(Peak);
        Glance.ActivateRailItem();
        Glance.Query = "bsn";

        Assert.False(Glance.RemoveLastChip());
        Assert.Single(Glance.Chips);
    }

    [Fact]
    public void Removing_a_chip_by_its_cross_ranks_without_it()
    {
        Pick(Peak);
        Glance.ActivateRailItem();

        Glance.RemoveChip(Glance.Chips[0]);

        Assert.Empty(Glance.Chips);
        Assert.Empty(_rig.Ranker.Asked[^1].CrateIds);
        Assert.False(CrateItem(7).IsActive);
    }

    [Fact]
    public void Picking_a_crate_clears_plain_typed_text_and_keeps_operators()
    {
        Glance.Query = "peak bpm:128 #vocal key:8A";
        Pick(Peak);

        Glance.ActivateRailItem();

        Assert.Equal("bpm:128 #vocal key:8A", Glance.Query);
        Assert.Equal("bpm:128 #vocal key:8A", _rig.Ranker.Asked[^1].Query);
    }

    [Fact]
    public void Chips_survive_a_close_and_reopen()
    {
        Pick(Peak);
        Glance.ActivateRailItem();
        Glance.Close();

        Glance.Open();

        Assert.Single(Glance.Chips);
        Assert.Equal([7], _rig.Ranker.Asked[^1].CrateIds);
    }

    [Fact]
    public void Rail_counts_follow_the_applied_result_not_the_request()
    {
        var gate = new TaskCompletionSource<RankedTracks>();
        _rig.Answer = _ => gate.Task;
        Pick(Peak);
        Glance.ActivateRailItem();
        Assert.Equal(9, CrateItem(8).Count);

        gate.SetResult(GlanceRig.Ranked(0, [Alpha]) with
        {
            ScopeCount = 12,
            ScopeCrateCounts = new Dictionary<int, int> { [7] = 12, [8] = 0 },
            ScopeTagCounts = new Dictionary<string, int> { ["vocal"] = 4 },
        });

        Assert.Equal(0, CrateItem(8).Count);
        Assert.True(CrateItem(8).IsZero);
        Assert.False(CrateItem(7).IsZero);
        Assert.True(CrateItem(7).IsActive);
        var tag = Glance.RailItems.OfType<GlanceRailTag>().Single();
        Assert.Equal(4, tag.Count);
        Assert.False(tag.IsZero);
        Assert.Equal(12, Glance.ScopeCount);
    }

    [Fact]
    public void Without_chips_the_rail_shows_each_item_s_own_size()
    {
        Assert.Equal(12, CrateItem(7).Count);
        Assert.False(CrateItem(7).IsZero);
        Assert.Equal(30, Glance.RailItems.OfType<GlanceRailTag>().Single().Count);
    }

    [Fact]
    public void Only_the_newest_chip_result_applies()
    {
        var first = new TaskCompletionSource<RankedTracks>();
        var second = new TaskCompletionSource<RankedTracks>();
        var pending = new Queue<TaskCompletionSource<RankedTracks>>([first, second]);
        _rig.Answer = _ => pending.Dequeue().Task;
        Pick(Peak);
        Glance.ActivateRailItem();
        Pick(Vocal);
        Glance.ActivateRailItem();

        second.SetResult(GlanceRig.Ranked(0, [Bravo]));
        first.SetResult(GlanceRig.Ranked(0, [Alpha]));

        Assert.Equal(["Bravo"], Glance.Rows.Select(r => r.Row.Title));
    }

    [Fact]
    public void The_empty_text_names_the_chips_and_the_text_from_the_applied_result()
    {
        Assert.Null(Glance.ScopeEmptyText);
        _rig.Answer = q => Task.FromResult(GlanceRig.Ranked(q.TargetDeck, []));
        Pick(Peak);
        Glance.ActivateRailItem();
        Assert.Equal("No tracks in Peak time", Glance.ScopeEmptyText);

        Pick(Vocal);
        Glance.ActivateRailItem();
        Assert.Equal("No tracks in Peak time tagged Vocal", Glance.ScopeEmptyText);

        Glance.Query = "bsn";
        Assert.Equal("No “bsn” in Peak time tagged Vocal", Glance.ScopeEmptyText);

        Glance.Query = "";
        Pick(Warm);
        Glance.ActivateRailItem();
        Pick(Vocal);
        Glance.ActivateRailItem();
        Assert.Equal("No tracks in Peak time and Warm up", Glance.ScopeEmptyText);

        Glance.RemoveLastChip();
        Glance.RemoveLastChip();
        Glance.RemoveLastChip();
        Assert.Null(Glance.ScopeEmptyText);
    }

    [Fact]
    public void The_action_text_says_add_or_remove_and_the_alternate_says_show_in_library()
    {
        Pick(Peak);
        Assert.Equal("Add filter", Glance.ActionText);
        Assert.Equal("Show in library", Glance.AlternateActionText);

        Glance.ActivateRailItem();

        Assert.Equal("Remove filter", Glance.ActionText);
    }

    [Fact]
    public void Rows_outside_the_visible_library_still_appear()
    {
        var elsewhere = GlanceRig.Track("Elsewhere");
        _rig.Answer = q => Task.FromResult(GlanceRig.Ranked(q.TargetDeck, [Alpha, elsewhere]));

        Glance.Query = "x";

        Assert.Equal(["Alpha", "Elsewhere"], Glance.Rows.Select(r => r.Row.Title));
    }

    [Fact]
    public void A_row_outside_the_library_follows_its_summary_changes()
    {
        var elsewhere = GlanceRig.Track("Elsewhere", bpm: 120);
        _rig.Answer = q => Task.FromResult(GlanceRig.Ranked(q.TargetDeck, [elsewhere]));
        Glance.Query = "x";
        var row = Glance.Rows[0].Row;

        _rig.Bus.Publish(new TrackSummaryChanged(elsewhere with { Bpm = 126 }));

        Assert.Equal(126, row.Bpm);
        Assert.Same(row, _rig.Library.RowFor(elsewhere));
    }

    [Fact]
    public void Reduced_motion_turns_off_animated_results()
    {
        Assert.True(Glance.AnimateResults);
        Assert.False(new GlanceRig(reducedMotion: true).Glance.AnimateResults);
    }

    [Fact]
    public void A_result_raises_ResultsReplaced()
    {
        var raised = 0;
        Glance.ResultsReplaced += () => raised++;

        Glance.Query = "x";

        Assert.Equal(1, raised);
    }
}

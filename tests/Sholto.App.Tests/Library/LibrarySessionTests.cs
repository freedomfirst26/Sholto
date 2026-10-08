using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Processing;
using Sholto.App.Library.Crates;
using Sholto.Data;
using Sholto.App.Library;

namespace Sholto.App.Tests;

/// <summary>The library session on its own, with no view model and no Avalonia: scan and hydration from
/// fake stores, the browse selection, tag and crate filters, BPM multiplier persistence, played tracking,
/// analysis progress and the "All Tracks" crate.</summary>
public class LibrarySessionTests
{
    private static FakeTagService Tags(params (Guid Id, string[] Tags)[] entries) =>
        new(entries.ToDictionary(e => e.Id, e => (IReadOnlyList<string>)e.Tags));

    // ---- Scan -----------------------------------------------------------------------------------

    [Fact]
    public async Task Scan_without_stores_lists_the_tracks_by_artist_then_title_with_no_catalog_ids()
    {
        var rig = new LibrarySessionRig();
        var announced = new List<IReadOnlyList<TrackSummary>>();
        rig.Library.RowsChanged += announced.Add;

        await rig.Library.ScanAsync("/music", null);

        Assert.Equal(
            new[] { LibrarySessionRig.Bravo.FilePath, LibrarySessionRig.Charlie.FilePath, LibrarySessionRig.Alpha.FilePath },
            rig.Library.Rows.Select(r => r.FilePath));
        Assert.All(rig.Library.Rows, r => Assert.Equal(Guid.Empty, r.TrackId));
        Assert.Equal("/music", rig.Library.CurrentDir);
        Assert.False(rig.Library.IsScanning);
        Assert.Single(announced);
        Assert.Equal(3, announced[0].Count);
    }

    [Fact]
    public async Task Scan_of_an_empty_folder_name_changes_nothing()
    {
        var rig = new LibrarySessionRig();
        var announced = 0;
        rig.Library.RowsChanged += _ => announced++;

        await rig.Library.ScanAsync("", null);

        Assert.Empty(rig.Library.Rows);
        Assert.Equal(0, announced);
    }

    [Fact]
    public async Task Scan_with_stores_stamps_ids_and_hydrates_bpm_multiplier_and_tags()
    {
        var rig = new LibrarySessionRig();
        var alphaId = rig.Catalog.Assign(LibrarySessionRig.Alpha.FilePath);
        var stack = rig.Stack(
            bpms: new Dictionary<string, double> { [LibrarySessionRig.Alpha.FilePath] = 128.5 },
            tags: Tags((alphaId, new[] { "house", "warm" })),
            multipliers: new FakeTempoMultiplierStore(
                new Dictionary<string, double> { [LibrarySessionRig.Alpha.FilePath] = 0.5 }));

        await rig.Library.ScanAsync("/music", stack);

        var alpha = rig.Row(LibrarySessionRig.Alpha);
        Assert.Equal(alphaId, alpha.TrackId);
        Assert.Equal(128.5, alpha.Bpm);
        Assert.Equal(0.5, alpha.BpmMultiplier);
        Assert.Equal(new[] { "house", "warm" }, alpha.Tags);
        var bravo = rig.Row(LibrarySessionRig.Bravo);
        Assert.NotEqual(Guid.Empty, bravo.TrackId);
        Assert.Null(bravo.Bpm);
        Assert.Equal(1.0, bravo.BpmMultiplier);
        Assert.Empty(bravo.Tags);
    }

    [Fact]
    public async Task A_scan_clears_the_unreachable_banner_and_keeps_the_shown_list_and_the_highlight()
    {
        var rig = new LibrarySessionRig();
        var alphaId = rig.Catalog.Assign(LibrarySessionRig.Alpha.FilePath);
        var tags = Tags((alphaId, new[] { "house" }));
        await rig.Library.ScanAsync("/music", rig.Stack(tags: tags));
        rig.Library.AttachServices(tags, rig.Crates);
        rig.Library.SetUnreachablePath("/media/gone");
        rig.Library.ShowTrackList([LibrarySessionRig.Alpha.FilePath]);
        rig.Library.Select(0);
        Assert.True(rig.Library.IsUnreachable);
        Assert.Single(rig.Library.Rows);

        await rig.Library.ScanAsync("/music", rig.Stack(tags: tags));

        Assert.False(rig.Library.IsUnreachable);
        Assert.Equal(new[] { LibrarySessionRig.Alpha.FilePath }, rig.Library.Rows.Select(r => r.FilePath));
        Assert.Equal(0, rig.Library.SelectedIndex);
    }

    [Fact]
    public void Setting_the_unreachable_path_announces_once_per_change()
    {
        var rig = new LibrarySessionRig();
        var changes = 0;
        rig.Library.UnreachableChanged += () => changes++;

        rig.Library.SetUnreachablePath("/media/gone");
        rig.Library.SetUnreachablePath("/media/gone");
        rig.Library.SetUnreachablePath(null);

        Assert.Equal(2, changes);
        Assert.False(rig.Library.IsUnreachable);
    }

    // ---- "All Tracks" crate ---------------------------------------------------------------------

    [Fact]
    public async Task Scan_files_every_track_into_All_Tracks_once_the_crate_service_is_attached()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", rig.Stack());
        Assert.Empty(rig.Crates.Members("All Tracks"));

        rig.Library.AttachServices(Tags(), rig.Crates);
        await rig.Library.ScanAsync("/music", rig.Stack());

        Assert.Equal(
            rig.Library.Rows.Select(r => r.TrackId).OrderBy(id => id),
            rig.Crates.Members("All Tracks").OrderBy(id => id));
    }

    [Fact]
    public void Attaching_the_services_announces_them_for_the_interface_to_build_its_editors_over()
    {
        var rig = new LibrarySessionRig();
        var tags = Tags();
        object? seenTags = null, seenCrates = null;
        rig.Library.ServicesAttached += (t, c) => { seenTags = t; seenCrates = c; };

        rig.Library.AttachServices(tags, rig.Crates);

        Assert.Same(tags, seenTags);
        Assert.Same(rig.Crates, seenCrates);
    }

    [Fact]
    public async Task A_tag_edit_updates_that_tracks_tags()
    {
        var rig = new LibrarySessionRig();
        var alphaId = rig.Catalog.Assign(LibrarySessionRig.Alpha.FilePath);
        var tags = Tags((alphaId, new[] { "house" }));
        await rig.Library.ScanAsync("/music", rig.Stack(tags: tags));
        rig.Library.AttachServices(tags, rig.Crates);
        var updated = new List<TrackSummary>();
        rig.Library.RowUpdated += updated.Add;

        tags.Edit(alphaId, "house", "peak");

        Assert.Equal(new[] { "house", "peak" }, rig.Row(LibrarySessionRig.Alpha).Tags);
        Assert.Single(updated);
    }

    // ---- Selection ------------------------------------------------------------------------------

    [Fact]
    public async Task The_browse_knob_starts_at_the_first_row_then_moves_and_clamps()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        Assert.Equal(-1, rig.Library.SelectedIndex);
        Assert.Null(rig.Library.SelectedTrack);

        rig.Library.Rotate(3);   // from "nothing" the first click lands on row 0, whatever its size
        Assert.Equal(0, rig.Library.SelectedIndex);
        Assert.Equal(LibrarySessionRig.Bravo, rig.Library.SelectedTrack);

        rig.Library.Rotate(1);
        Assert.Equal(1, rig.Library.SelectedIndex);
        rig.Library.Rotate(10);
        Assert.Equal(2, rig.Library.SelectedIndex);
        rig.Library.Rotate(-10);
        Assert.Equal(0, rig.Library.SelectedIndex);
    }

    [Fact]
    public void The_browse_knob_does_nothing_with_no_rows()
    {
        var rig = new LibrarySessionRig();

        rig.Library.Rotate(1);
        rig.Library.Select(5);

        Assert.Equal(-1, rig.Library.SelectedIndex);
    }

    [Fact]
    public async Task A_selection_outside_the_visible_rows_selects_no_track()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);

        rig.Library.SetSelectedIndex(7);

        Assert.Equal(7, rig.Library.SelectedIndex);
        Assert.Null(rig.Library.SelectedTrack);
        Assert.Null(rig.Library.SelectedSummary);
    }

    [Fact]
    public async Task A_selection_change_is_announced_to_listeners_and_on_the_bus()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", rig.Stack());
        var onBus = new RecordingHandler<SelectionChanged>();
        using var subscription = rig.Bus.Subscribe(onBus);
        var changes = 0;
        rig.Library.SelectedIndexChanged += () => changes++;

        rig.Library.Select(1);
        rig.Library.Select(1);

        Assert.Equal(1, changes);
        Assert.Equal(1, onBus.Received.Last().Index);
        Assert.Equal(rig.Row(LibrarySessionRig.Charlie).TrackId, onBus.Received.Last().TrackId);
    }

    [Fact]
    public async Task The_visible_rows_are_announced_on_the_bus_with_their_count()
    {
        var rig = new LibrarySessionRig();
        var onBus = new RecordingHandler<LibraryRowsChanged>();
        using var subscription = rig.Bus.Subscribe(onBus);

        await rig.Library.ScanAsync("/music", null);

        Assert.Equal(3, onBus.Received.Last().Rows.Count);
    }

    // ---- Shown list -----------------------------------------------------------------------------

    [Fact]
    public async Task Showing_a_list_replaces_the_rows_in_that_order_and_skips_paths_the_catalog_lacks()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        var onBus = new RecordingHandler<LibraryRowsChanged>();
        using var sub = rig.Bus.Subscribe(onBus);

        rig.Library.ShowTrackList([LibrarySessionRig.Alpha.FilePath, "/gone.mp3", LibrarySessionRig.Bravo.FilePath]);

        Assert.Equal(
            new[] { LibrarySessionRig.Alpha.FilePath, LibrarySessionRig.Bravo.FilePath },
            rig.Library.Rows.Select(r => r.FilePath));
        Assert.Equal(2, onBus.Received[^1].Rows.Count);
    }

    [Fact]
    public async Task Showing_an_empty_list_shows_no_rows()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);

        rig.Library.ShowTrackList([]);

        Assert.Empty(rig.Library.Rows);
        Assert.Equal(3, rig.Library.Catalog.Count);
    }

    [Fact]
    public async Task The_highlight_stays_on_its_path_when_the_list_is_reordered()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        rig.Library.Select(2);
        Assert.Equal(LibrarySessionRig.Alpha.FilePath, rig.Library.SelectedSummary!.FilePath);

        rig.Library.ShowTrackList([LibrarySessionRig.Alpha.FilePath, LibrarySessionRig.Bravo.FilePath]);

        Assert.Equal(0, rig.Library.SelectedIndex);
        Assert.Equal(LibrarySessionRig.Alpha.FilePath, rig.Library.SelectedSummary!.FilePath);
    }

    [Fact]
    public async Task When_the_highlighted_path_leaves_the_list_the_same_index_is_kept_clamped()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        rig.Library.Select(2);

        rig.Library.ShowTrackList([LibrarySessionRig.Bravo.FilePath, LibrarySessionRig.Charlie.FilePath]);

        Assert.Equal(1, rig.Library.SelectedIndex);
    }

    [Fact]
    public async Task A_fact_that_lands_while_a_list_is_shown_is_there_when_the_whole_catalog_shows_again()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        rig.Library.ShowTrackList([LibrarySessionRig.Bravo.FilePath]);

        rig.Library.SeedKnownBpms(new Dictionary<string, double> { [LibrarySessionRig.Alpha.FilePath] = 90.0 });
        rig.Library.ShowTrackList(rig.Library.Catalog.Select(r => r.FilePath).ToList());

        Assert.Equal(90.0, rig.Row(LibrarySessionRig.Alpha).Bpm);
    }

    // ---- BPM multiplier -------------------------------------------------------------------------

    [Fact]
    public async Task A_multiplier_chosen_on_a_deck_updates_the_row_and_is_persisted_once_the_store_is_attached()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", rig.Stack());
        var store = new FakeTempoMultiplierStore();
        rig.Library.AttachMultiplierStore(store);
        rig.Deck1.LoadTrack(LibrarySessionRig.Alpha, LibrarySessionRig.Alpha.FilePath, [], bpmMultiplier: 1.0);

        rig.Deck1.HalveBpm();

        Assert.Equal(new[] { (LibrarySessionRig.Alpha.FilePath, 0.5) }, store.Puts);
        Assert.Equal(0.5, rig.Library.GetBpmMultiplierFor(LibrarySessionRig.Alpha.FilePath));
        Assert.Equal(0.5, rig.Row(LibrarySessionRig.Alpha).BpmMultiplier);
    }

    [Fact]
    public async Task Without_a_database_a_chosen_multiplier_shows_on_the_row_and_nothing_is_persisted()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        rig.Deck2.LoadTrack(LibrarySessionRig.Bravo, LibrarySessionRig.Bravo.FilePath, [], bpmMultiplier: 1.0);

        rig.Deck2.DoubleBpm();

        Assert.Equal(2.0, rig.Row(LibrarySessionRig.Bravo).BpmMultiplier);
        Assert.Empty(rig.Multipliers.Puts);
    }

    [Fact]
    public async Task A_multiplier_that_was_saved_comes_back_with_the_next_scan()
    {
        var rig = new LibrarySessionRig();
        var store = new FakeTempoMultiplierStore();
        await rig.Library.ScanAsync("/music", rig.Stack(multipliers: store));
        rig.Library.AttachMultiplierStore(store);
        rig.Deck1.LoadTrack(LibrarySessionRig.Alpha, LibrarySessionRig.Alpha.FilePath, [], bpmMultiplier: 1.0);
        rig.Deck1.HalveBpm();
        var saved = new Dictionary<string, double>();
        foreach (var (path, multiplier) in store.Puts) saved[path] = multiplier;

        await rig.Library.ScanAsync("/music", rig.Stack(multipliers: new FakeTempoMultiplierStore(saved)));

        Assert.Equal(0.5, rig.Row(LibrarySessionRig.Alpha).BpmMultiplier);
    }

    // ---- Played, analysis, seeding --------------------------------------------------------------

    [Fact]
    public async Task A_track_loaded_into_a_deck_is_marked_played_once()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        var updated = new List<TrackSummary>();
        rig.Library.RowUpdated += updated.Add;

        rig.Deck2.LoadTrack(LibrarySessionRig.Bravo, LibrarySessionRig.Bravo.FilePath, [], bpmMultiplier: 1.0);
        rig.Deck1.LoadTrack(LibrarySessionRig.Bravo, LibrarySessionRig.Bravo.FilePath, [], bpmMultiplier: 1.0);

        Assert.True(rig.Row(LibrarySessionRig.Bravo).IsPlayed);
        Assert.False(rig.Row(LibrarySessionRig.Alpha).IsPlayed);
        Assert.Single(updated.Where(s => s.IsPlayed));
    }

    [Fact]
    public async Task Analysis_progress_and_failure_show_on_the_track_and_progress_ticks_that_change_nothing_stay_quiet()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        var path = LibrarySessionRig.Alpha.FilePath;
        var updated = new List<TrackSummary>();
        rig.Library.RowUpdated += updated.Add;

        rig.Reporter.Running(path, AnalysisSteps.Beats, 0.2, "20%");
        rig.Reporter.Running(path, AnalysisSteps.Beats, 0.4, "40%");
        Assert.True(rig.Row(LibrarySessionRig.Alpha).IsAnalyzing);
        Assert.Single(updated);

        rig.Reporter.Failed(path, AnalysisSteps.Beats, "madmom exited with code 1");
        var row = rig.Row(LibrarySessionRig.Alpha);
        Assert.False(row.IsAnalyzing);
        Assert.Contains("madmom exited with code 1", row.AnalysisFailure);
        Assert.True(row.HasRequiredFailure);
    }

    [Fact]
    public async Task Seeding_puts_stored_bpms_multipliers_and_keys_on_the_tracks()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        var key = new Key(PitchClass: 0, IsMajor: true);

        rig.Library.SeedKnownBpms(new Dictionary<string, double> { [LibrarySessionRig.Charlie.FilePath] = 124.0 });
        rig.Library.SeedKnownBpmMultipliers(new Dictionary<string, double> { [LibrarySessionRig.Charlie.FilePath] = 2.0 });
        rig.Library.SeedKnownKeys(new Dictionary<string, Key> { [LibrarySessionRig.Charlie.FilePath] = key });

        var charlie = rig.Row(LibrarySessionRig.Charlie);
        Assert.Equal(124.0, charlie.Bpm);
        Assert.Equal(2.0, charlie.BpmMultiplier);
        Assert.Equal((KeyRef?)key.ToRef(), charlie.MusicalKey);
        Assert.Null(rig.Row(LibrarySessionRig.Alpha).Bpm);
    }

    [Fact]
    public async Task A_forced_reanalysis_updates_bpm_and_key_or_shows_its_failure()
    {
        var rig = new LibrarySessionRig();
        await rig.Library.ScanAsync("/music", null);
        var path = LibrarySessionRig.Alpha.FilePath;
        var key = new Key(PitchClass: 3, IsMajor: false);

        rig.Library.ApplyReanalysis(path, 99.5, key);
        Assert.Equal(99.5, rig.Row(LibrarySessionRig.Alpha).Bpm);
        Assert.Equal((KeyRef?)key.ToRef(), rig.Row(LibrarySessionRig.Alpha).MusicalKey);

        rig.Library.ReportReanalysisFailure(path, "IOException: no such file");
        Assert.Equal("IOException: no such file", rig.Row(LibrarySessionRig.Alpha).AnalysisFailure);
    }

    [Fact]
    public void Loading_a_key_into_deck_1_publishes_the_keys_that_mix_with_it()
    {
        var rig = new LibrarySessionRig();
        var published = new RecordingHandler<HarmonyReferenceChanged>();
        rig.Bus.Subscribe(published);
        published.Received.Clear();

        rig.Deck1.Analysis.Set(new KeyAnalysis(new Key(PitchClass: 0, IsMajor: true)));   // 8B
        rig.Library.RefreshHarmonyReference();

        var e = published.Received[^1];
        Assert.Equal(new KeyRef(0, true), e.Key);
        Assert.Equal(
            new[] { "7A", "7B", "8A", "8B", "9A", "9B" },
            e.MixableKeys.Select(k => k.ToCamelot()).OrderBy(c => c).ToArray());
    }
}

using Sholto.App.Analysis.Harmony;
using Sholto.App.Library;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The library list as the screen shows it: <see cref="TrackRow"/>s kept in step with the library
/// session's bus events, with every bound property name and type unchanged.</summary>
public class LibraryRowsViewModelTests
{
    private static (LibrarySessionRig Rig, LibraryRowsViewModel Rows) New()
    {
        AvaloniaTestApp.EnsureStarted();
        var rig = new LibrarySessionRig();
        var rows = new LibraryRowsViewModel(rig.Bus, new TrackRowFactory(new ThemeStackFactory().Build().Context));
        return (rig, rows);
    }

    [Fact]
    public async Task The_rows_follow_the_session_after_a_scan_in_display_order()
    {
        var (rig, rows) = New();

        await rig.Library.ScanAsync("/music", null);

        Assert.Equal(new[] { "Bravo", "Charlie", "Alpha" }, rows.Items.Select(r => r.Title));
        Assert.Equal(new[] { "Abe", "Abe", "Zed" }, rows.Items.Select(r => r.Artist));
        Assert.Equal("03:00", rows.Items[2].DurationDisplay);
    }

    [Fact]
    public async Task A_fact_that_lands_updates_the_same_row_and_notifies_the_bound_properties()
    {
        var (rig, rows) = New();
        await rig.Library.ScanAsync("/music", null);
        var alpha = rows.Items.Single(r => r.Title == "Alpha");
        var changed = new List<string?>();
        alpha.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        rig.Library.SeedKnownBpms(new Dictionary<string, double> { [LibrarySessionRig.Alpha.FilePath] = 128.0 });
        rig.Library.SeedKnownBpmMultipliers(new Dictionary<string, double> { [LibrarySessionRig.Alpha.FilePath] = 0.5 });

        Assert.Same(alpha, rows.Items.Single(r => r.Title == "Alpha"));
        Assert.Equal(128.0, alpha.Bpm);
        Assert.Equal(0.5, alpha.BpmMultiplier);
        Assert.NotEqual("", alpha.BpmDisplay);
        Assert.Contains(nameof(TrackRow.BpmDisplay), changed);
        Assert.Contains(nameof(TrackRow.AnalysisState), changed);
    }

    [Fact]
    public async Task A_row_shows_the_tracks_key_and_played_state_and_tags()
    {
        var (rig, rows) = New();
        await rig.Library.ScanAsync("/music", null);
        var key = new Key(PitchClass: 0, IsMajor: true);

        rig.Library.SeedKnownKeys(new Dictionary<string, Key> { [LibrarySessionRig.Bravo.FilePath] = key });
        rig.Deck1.LoadTrack(LibrarySessionRig.Bravo, LibrarySessionRig.Bravo.FilePath, [], bpmMultiplier: 1.0);

        var bravo = rows.Items.Single(r => r.Title == "Bravo");
        Assert.Equal(key.ToCamelot(), bravo.Key);
        Assert.True(bravo.IsPlayed);
        Assert.False(rows.Items.Single(r => r.Title == "Alpha").IsPlayed);
    }

    [Fact]
    public async Task A_filter_keeps_the_rows_that_stay_and_clearing_it_brings_all_back_in_order()
    {
        var (rig, rows) = New();
        var charlieId = rig.Catalog.Assign(LibrarySessionRig.Charlie.FilePath);
        var tags = new FakeTagService(new Dictionary<Guid, IReadOnlyList<string>>
        {
            [charlieId] = new[] { "peak" },
        });
        await rig.Library.ScanAsync("/music", rig.Stack(tags: tags));
        rig.Library.AttachServices(tags, rig.Crates);
        var charlie = rows.Items.Single(r => r.Title == "Charlie");

        await rig.Library.FilterByTagAsync("peak");

        Assert.Same(charlie, Assert.Single(rows.Items));

        rig.Library.ClearFilter();

        Assert.Equal(new[] { "Bravo", "Charlie", "Alpha" }, rows.Items.Select(r => r.Title));
        Assert.Same(charlie, rows.Items.Single(r => r.Title == "Charlie"));
    }

    [Fact]
    public async Task A_rescan_rebuilds_the_rows_from_the_catalog_and_resets_the_selection()
    {
        var (rig, rows) = New();
        await rig.Library.ScanAsync("/music", null);
        rig.Library.Select(1);

        await rig.Library.ScanAsync("/music", null);

        Assert.Equal(3, rows.Items.Count);
        Assert.Equal(-1, rig.Library.SelectedIndex);
    }

    [Fact]
    public async Task The_row_for_a_summary_carries_every_field_the_library_binds()
    {
        var (rig, rows) = New();
        var alphaId = rig.Catalog.Assign(LibrarySessionRig.Alpha.FilePath);
        var tags = new FakeTagService(new Dictionary<Guid, IReadOnlyList<string>>
        {
            [alphaId] = new[] { "house", "warm" },
        });
        await rig.Library.ScanAsync("/music", rig.Stack(tags: tags));

        var alpha = rows.Items.Single(r => r.Title == "Alpha");

        Assert.Equal(alphaId, alpha.TrackId);
        Assert.Equal(2, alpha.TagCount);
        Assert.True(alpha.HasTags);
        Assert.Equal("house, warm", alpha.TagsTooltip);
        Assert.Equal(TrackAnalysisState.Unanalyzed, alpha.AnalysisState);
        Assert.Equal(LibrarySessionRig.Alpha.FilePath, alpha.Summary.FilePath);
    }
}

using Sholto.Data;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The library rows view model driven purely by events on a bus, with no library session: rows
/// built in order and kept across a refresh, per-track facts applied to the matching row only, the harmony
/// reference carried by every row, and the picture replayed to a late subscriber.</summary>
public class LibraryRowsViewModelBusTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly IThemeContext _theme;
    private readonly LibraryRowsViewModel _rows;

    public LibraryRowsViewModelBusTests()
    {
        AvaloniaTestApp.EnsureStarted();
        _theme = new ThemeStackFactory().Build().Context;
        _rows = NewRows();
    }

    private LibraryRowsViewModel NewRows() => new(_bus, new TrackRowFactory(_theme));

    private TrackSummary Summary(string title, string artist = "Artist") =>
        new($"/music/{title}.mp3", title, artist, TimeSpan.FromMinutes(3)) { TrackId = Guid.NewGuid() };

    private void Show(int version, params TrackSummary[] rows) => _bus.Publish(new LibraryRowsChanged(rows, version));

    [Fact]
    public void Rows_are_built_in_the_order_they_arrive()
    {
        Show(1, Summary("Charlie"), Summary("Alpha"), Summary("Bravo"));

        Assert.Equal(["Charlie", "Alpha", "Bravo"], _rows.Items.Select(r => r.Title));
    }

    [Fact]
    public void A_row_carries_the_summarys_facts()
    {
        var summary = Summary("Alpha", "Zed") with { Bpm = 128.0, IsPlayed = true, Tags = ["peak"] };

        Show(1, summary);

        var row = _rows.Items.Single();
        Assert.Equal("Zed", row.Artist);
        Assert.Equal(summary.TrackId, row.TrackId);
        Assert.Equal(128.0, row.Bpm);
        Assert.True(row.IsPlayed);
        Assert.Equal(["peak"], row.Tags);
        Assert.Equal("03:00", row.DurationDisplay);
    }

    [Fact]
    public void A_track_that_reappears_keeps_its_row_and_the_new_order_is_applied()
    {
        var alpha = Summary("Alpha");
        var bravo = Summary("Bravo");
        Show(1, alpha, bravo);
        var alphaRow = _rows.Items[0];
        var bravoRow = _rows.Items[1];

        Show(2, bravo, alpha);

        Assert.Same(bravoRow, _rows.Items[0]);
        Assert.Same(alphaRow, _rows.Items[1]);
    }

    [Fact]
    public void A_kept_row_is_brought_up_to_date_with_the_new_summary()
    {
        var alpha = Summary("Alpha");
        Show(1, alpha);
        var row = _rows.Items.Single();

        Show(2, alpha with { Bpm = 90.0, StemsReady = true });

        Assert.Same(row, _rows.Items.Single());
        Assert.Equal(90.0, row.Bpm);
        Assert.True(row.StemsReady);
    }

    [Fact]
    public void A_track_that_drops_out_loses_its_row_and_one_that_returns_gets_a_new_one()
    {
        var alpha = Summary("Alpha");
        var bravo = Summary("Bravo");
        Show(1, alpha, bravo);
        var bravoRow = _rows.Items[1];

        Show(2, alpha);
        Assert.Equal(["Alpha"], _rows.Items.Select(r => r.Title));

        Show(3, alpha, bravo);
        Assert.Equal(["Alpha", "Bravo"], _rows.Items.Select(r => r.Title));
        Assert.NotSame(bravoRow, _rows.Items[1]);
    }

    [Fact]
    public void A_file_with_changed_scanned_metadata_gets_a_new_row()
    {
        var alpha = Summary("Alpha", "Zed");
        Show(1, alpha);
        var row = _rows.Items.Single();

        Show(2, alpha with { Artist = "Someone Else" });

        Assert.NotSame(row, _rows.Items.Single());
        Assert.Equal("Someone Else", _rows.Items.Single().Artist);
    }

    [Fact]
    public void An_empty_list_empties_the_rows()
    {
        Show(1, Summary("Alpha"));

        Show(2);

        Assert.Empty(_rows.Items);
    }

    [Fact]
    public void A_track_summary_updates_the_matching_row_only()
    {
        var alpha = Summary("Alpha");
        var bravo = Summary("Bravo");
        Show(1, alpha, bravo);
        var alphaRow = _rows.Items[0];
        var bravoRow = _rows.Items[1];
        var alphaChanged = new List<string?>();
        alphaRow.PropertyChanged += (_, e) => alphaChanged.Add(e.PropertyName);

        _bus.Publish(new TrackSummaryChanged(bravo with { Bpm = 140.0, IsPlayed = true }));

        Assert.Equal(140.0, bravoRow.Bpm);
        Assert.True(bravoRow.IsPlayed);
        Assert.Null(alphaRow.Bpm);
        Assert.False(alphaRow.IsPlayed);
        Assert.Empty(alphaChanged);
        Assert.Same(bravoRow, _rows.Items[1]);
    }

    [Fact]
    public void A_track_summary_for_a_file_that_is_not_shown_adds_no_row()
    {
        Show(1, Summary("Alpha"));

        _bus.Publish(new TrackSummaryChanged(Summary("Hidden") with { Bpm = 100.0 }));

        Assert.Equal(["Alpha"], _rows.Items.Select(r => r.Title));
    }

    [Fact]
    public void A_summary_that_arrives_before_the_row_is_not_remembered()
    {
        var alpha = Summary("Alpha");
        _bus.Publish(new TrackSummaryChanged(alpha with { Bpm = 100.0 }));

        Show(1, alpha);

        Assert.Null(_rows.Items.Single().Bpm);
    }

    [Fact]
    public void A_summarys_key_ref_becomes_the_rows_musical_key()
    {
        var alpha = Summary("Alpha");
        Show(1, alpha);
        var row = _rows.Items.Single();
        Assert.Null(row.MusicalKey);

        _bus.Publish(new TrackSummaryChanged(alpha with { MusicalKey = new KeyRef(0, true) }));

        Assert.Equal(new KeyRef(0, true), row.MusicalKey);
        Assert.Equal(new KeyRef(0, true).ToCamelot(), row.Key);
    }

    [Fact]
    public void A_key_in_the_rows_summary_shows_on_a_newly_built_row()
    {
        Show(1, Summary("Alpha") with { MusicalKey = new KeyRef(5, false) });

        Assert.Equal(new KeyRef(5, false), _rows.Items.Single().MusicalKey);
    }

    private static HarmonyReferenceChanged Reference(KeyRef key, params KeyRef[] mixable) =>
        new(key, mixable);

    [Fact]
    public void The_mixable_keys_are_set_on_every_row()
    {
        Show(1, Summary("Alpha"), Summary("Bravo"));
        var mixable = new[] { new KeyRef(2, true), new KeyRef(7, true) };

        _bus.Publish(new HarmonyReferenceChanged(new KeyRef(2, true), mixable));

        Assert.All(_rows.Items, r => Assert.Equal(mixable, r.MixableKeys));
    }

    [Fact]
    public void A_row_created_after_the_reference_carries_it()
    {
        _bus.Publish(Reference(new KeyRef(2, true), new KeyRef(2, true)));

        Show(1, Summary("Alpha"));

        Assert.Equal([new KeyRef(2, true)], _rows.Items.Single().MixableKeys);
    }

    [Fact]
    public void Clearing_the_harmony_reference_clears_it_on_every_row()
    {
        _bus.Publish(Reference(new KeyRef(2, true), new KeyRef(2, true)));
        Show(1, Summary("Alpha"), Summary("Bravo"));

        _bus.Publish(new HarmonyReferenceChanged(null, []));

        Assert.All(_rows.Items, r => Assert.Empty(r.MixableKeys));
    }

    [Fact]
    public void A_row_is_eligible_only_when_its_key_is_among_the_mixable_keys()
    {
        Show(1,
            Summary("Alpha") with { MusicalKey = new KeyRef(0, true) },   // 8B
            Summary("Bravo") with { MusicalKey = new KeyRef(7, true) });   // 9B
        _bus.Publish(Reference(new KeyRef(0, true), new KeyRef(0, true)));

        Assert.True(_rows.Items[0].KeyEligible);
        Assert.False(_rows.Items[1].KeyEligible);
    }

    [Fact]
    public void A_late_subscriber_is_replayed_the_rows_and_the_reference()
    {
        _bus.Publish(Reference(new KeyRef(7, false), new KeyRef(7, false)));
        Show(1, Summary("Alpha"), Summary("Bravo"));

        var late = NewRows();

        Assert.Equal(["Alpha", "Bravo"], late.Items.Select(r => r.Title));
        Assert.All(late.Items, r => Assert.Equal([new KeyRef(7, false)], r.MixableKeys));
    }

    [Fact]
    public void A_late_subscriber_gets_the_latest_rows_only()
    {
        Show(1, Summary("Alpha"));
        Show(2, Summary("Bravo"), Summary("Charlie"));

        var late = NewRows();

        Assert.Equal(["Bravo", "Charlie"], late.Items.Select(r => r.Title));
    }

    [Fact]
    public void A_rows_summary_is_the_latest_one_announced()
    {
        Show(1, Summary("Alpha"));
        var updated = _rows.Items.Single().Summary with { Bpm = 124.0 };

        _bus.Publish(new TrackSummaryChanged(updated));

        Assert.Same(updated, _rows.Items.Single().Summary);
    }

    [Fact]
    public void Two_view_models_on_one_bus_each_keep_their_own_rows()
    {
        var other = NewRows();

        Show(1, Summary("Alpha"));

        Assert.NotSame(_rows.Items[0], other.Items[0]);
        Assert.Equal(_rows.Items[0].FilePath, other.Items[0].FilePath);
    }
}

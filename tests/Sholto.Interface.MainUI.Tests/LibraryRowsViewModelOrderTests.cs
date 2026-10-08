using System.Collections.Specialized;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>A new row picture is applied as minimal edits to the list (move, insert, remove) with the same
/// <see cref="TrackRow"/> instances, so a reorder does not make the list jump or lose its scroll position.</summary>
public class LibraryRowsViewModelOrderTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly LibraryRowsViewModel _rows;
    private readonly List<NotifyCollectionChangedEventArgs> _changes = [];

    public LibraryRowsViewModelOrderTests()
    {
        AvaloniaTestApp.EnsureStarted();
        _rows = new LibraryRowsViewModel(_bus, new TrackRowFactory(new ThemeStackFactory().Build().Context));
        _rows.Items.CollectionChanged += (_, e) => _changes.Add(e);
    }

    private TrackSummary Summary(string title) =>
        new($"/music/{title}.mp3", title, "Artist", TimeSpan.FromMinutes(3)) { TrackId = Guid.NewGuid() };

    private void Show(int version, params TrackSummary[] rows) => _bus.Publish(new LibraryRowsChanged(rows, version));

    [Fact]
    public void A_reorder_keeps_the_same_row_instances_and_raises_a_Move_not_a_Reset()
    {
        var (a, b, c, d) = (Summary("A"), Summary("B"), Summary("C"), Summary("D"));
        Show(1, a, b, c, d);
        var before = _rows.Items.ToArray();
        _changes.Clear();

        Show(2, b, c, d, a);

        Assert.Equal([before[1], before[2], before[3], before[0]], _rows.Items);
        Assert.All(_rows.Items, r => Assert.Contains(r, before));
        Assert.DoesNotContain(_changes, e => e.Action == NotifyCollectionChangedAction.Reset);
        Assert.Contains(_changes, e => e.Action == NotifyCollectionChangedAction.Move);
        Assert.DoesNotContain(_changes, e => e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Remove);
    }

    [Fact]
    public void A_kept_row_whose_analysis_landed_shows_the_tick_on_the_same_instance()
    {
        var (a, b) = (Summary("A"), Summary("Ike's School"));
        Show(1, a, b);
        var rowB = _rows.Items[1];
        Assert.False(rowB.ShowAnalyzedCheck);

        Show(2, a, b with { Bpm = 120, StemsReady = true });

        Assert.Same(rowB, _rows.Items[1]);
        Assert.True(rowB.ShowAnalyzedCheck);
    }

    [Fact]
    public void A_row_moved_up_is_one_Move()
    {
        var (a, b, c, d) = (Summary("A"), Summary("B"), Summary("C"), Summary("D"));
        Show(1, a, b, c, d);
        _changes.Clear();

        Show(2, a, d, b, c);

        Assert.Equal(["A", "D", "B", "C"], _rows.Items.Select(r => r.Title));
        var move = Assert.Single(_changes);
        Assert.Equal(NotifyCollectionChangedAction.Move, move.Action);
    }

    [Fact]
    public void Added_and_removed_rows_are_inserted_and_removed_in_place()
    {
        var (a, b, c, d) = (Summary("A"), Summary("B"), Summary("C"), Summary("D"));
        Show(1, a, b, c);
        var rowA = _rows.Items[0];
        var rowC = _rows.Items[2];
        _changes.Clear();

        Show(2, a, d, c);

        Assert.Equal(["A", "D", "C"], _rows.Items.Select(r => r.Title));
        Assert.Same(rowA, _rows.Items[0]);
        Assert.Same(rowC, _rows.Items[2]);
        Assert.DoesNotContain(_changes, e => e.Action == NotifyCollectionChangedAction.Reset);
        Assert.Same(_rows.Items[1], _rows.RowFor("/music/D.mp3"));
        Assert.Null(_rows.RowFor("/music/B.mp3"));
    }

    [Fact]
    public void A_shuffle_ends_in_the_requested_order()
    {
        var all = "ABCDEFG".Select(ch => Summary(ch.ToString())).ToArray();
        Show(1, all);

        var order = new[] { 4, 0, 6, 2, 1, 5, 3 }.Select(i => all[i]).ToArray();
        Show(2, order);

        Assert.Equal(order.Select(s => s.Title), _rows.Items.Select(r => r.Title));
    }
}

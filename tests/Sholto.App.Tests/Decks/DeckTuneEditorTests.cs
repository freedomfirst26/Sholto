using Sholto.App.Decks;

namespace Sholto.App.Tests;

/// <summary>The tune editor and two-point grid edit over a spy beatgrid.</summary>
public class DeckTuneEditorTests
{
    private readonly F9SpyBeatgrid _beatgrid = new();
    private readonly DeckTuneEditor _editor;
    private readonly List<DeckChange> _changes = [];

    public DeckTuneEditorTests()
    {
        _editor = new DeckTuneEditor(_beatgrid);
        _editor.Changed += _changes.Add;
    }

    [Fact]
    public void Toggle_open_and_close_raise_EditOpen_only_on_a_change()
    {
        _editor.ToggleEdit();
        Assert.True(_editor.EditOpen);
        _editor.OpenEdit();
        _editor.CloseEdit();
        Assert.False(_editor.EditOpen);
        _editor.CloseEdit();

        Assert.Equal([DeckChange.EditOpen, DeckChange.EditOpen], _changes);
    }

    [Fact]
    public void A_click_does_nothing_unless_grid_edit_is_active()
    {
        _editor.OnGridClick(5.0);
        _editor.OnGridClick(9.0);

        Assert.Empty(_beatgrid.TwoPointGrids);
        Assert.Empty(_changes);
    }

    [Fact]
    public void Two_clicks_set_the_grid_and_leave_grid_edit()
    {
        _editor.ToggleGridEdit();
        _editor.OnGridClick(5.0);
        Assert.Empty(_beatgrid.TwoPointGrids);
        _editor.OnGridClick(9.0);

        Assert.Equal([(5.0, 9.0)], _beatgrid.TwoPointGrids);
        Assert.False(_editor.GridEditActive);
        Assert.Equal([DeckChange.GridEdit, DeckChange.GridEdit], _changes);
    }

    [Fact]
    public void Toggling_grid_edit_discards_a_pending_anchor()
    {
        _editor.ToggleGridEdit();
        _editor.OnGridClick(5.0);
        _editor.ToggleGridEdit();
        _editor.ToggleGridEdit();
        _editor.OnGridClick(9.0);
        Assert.Empty(_beatgrid.TwoPointGrids);
        _editor.OnGridClick(12.0);

        Assert.Equal([(9.0, 12.0)], _beatgrid.TwoPointGrids);
    }
}

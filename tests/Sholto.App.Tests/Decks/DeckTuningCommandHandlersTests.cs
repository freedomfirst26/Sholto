using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App.Tests;

/// <summary>The tune-editor commands against two real deck sessions whose beatgrids are spies: the BPM
/// multiplier ops change the multiplier as the session does, the grid commands reach the beatgrid of the
/// deck they name, and the editor commands open and close that deck's tune editor.</summary>
public class DeckTuningCommandHandlersTests
{
    private readonly Origin _from = new(InterfaceIds.Bench, "test", "tune");
    private readonly F9SpyBeatgrid _beatgrid1 = new();
    private readonly F9SpyBeatgrid _beatgrid2 = new();
    private readonly IDeckSession _deck1;
    private readonly IDeckSession _deck2;
    private readonly DeckTuningCommandHandlers _handlers;

    public DeckTuningCommandHandlersTests()
    {
        _deck1 = NewSession(_beatgrid1, sourceBpm: 128.0);
        _deck2 = NewSession(_beatgrid2, sourceBpm: null);
        _handlers = new DeckTuningCommandHandlers(new DeckPair(_deck1, _deck2));
    }

    private BasicAnalysis MakeBasic(double bpm) => new(
        new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000),
        Bpm: bpm,
        BeatTimes: [],
        DownbeatTimes: []);

    /// <summary>A session whose beatgrid is <paramref name="beatgrid"/>; basic analysis is set BEFORE the
    /// session subscribes, so no analysis event fires against it.</summary>
    private IDeckSession NewSession(F9SpyBeatgrid beatgrid, double? sourceBpm)
    {
        var scripted = new ScriptedPorts(new TestDeckFactory().Create());
        if (sourceBpm is { } bpm) scripted.Loading.Analysis.Set(MakeBasic(bpm));
        return new DeckSessionRig(new F9BeatgridPorts(scripted, beatgrid)).Session;
    }

    // ---- ChangeBpmMultiplier --------------------------------------------------------------------

    [Fact]
    public void Halve_halves_the_multiplier()
    {
        _handlers.Handle(new ChangeBpmMultiplier(0, BpmMultiplierOp.Halve, _from));

        Assert.Equal(0.5, _deck1.BpmMultiplier);
    }

    [Fact]
    public void Double_doubles_the_multiplier()
    {
        _handlers.Handle(new ChangeBpmMultiplier(0, BpmMultiplierOp.Double, _from));

        Assert.Equal(2.0, _deck1.BpmMultiplier);
    }

    [Fact]
    public void Reset_returns_the_multiplier_to_unity()
    {
        _handlers.Handle(new ChangeBpmMultiplier(0, BpmMultiplierOp.Double, _from));

        _handlers.Handle(new ChangeBpmMultiplier(0, BpmMultiplierOp.Reset, _from));

        Assert.Equal(1.0, _deck1.BpmMultiplier);
    }

    [Fact]
    public void Toggle_halves_a_fast_track_then_returns_to_unity()
    {
        _handlers.Handle(new ChangeBpmMultiplier(0, BpmMultiplierOp.Toggle, _from));
        Assert.Equal(0.5, _deck1.BpmMultiplier);   // 128 bpm: likely doubled by the analyser

        _handlers.Handle(new ChangeBpmMultiplier(0, BpmMultiplierOp.Toggle, _from));
        Assert.Equal(1.0, _deck1.BpmMultiplier);
    }

    [Fact]
    public void Toggle_doubles_a_slow_or_unanalysed_track()
    {
        _handlers.Handle(new ChangeBpmMultiplier(1, BpmMultiplierOp.Toggle, _from));

        Assert.Equal(2.0, _deck2.BpmMultiplier);
    }

    [Fact]
    public void A_multiplier_change_reaches_only_the_deck_it_names()
    {
        _handlers.Handle(new ChangeBpmMultiplier(1, BpmMultiplierOp.Halve, _from));

        Assert.Equal(0.5, _deck2.BpmMultiplier);
        Assert.Equal(1.0, _deck1.BpmMultiplier);
    }

    // ---- ResetDeckToAnalysis --------------------------------------------------------------------

    [Fact]
    public void Resetting_to_analysis_clears_the_grid_and_the_multiplier()
    {
        _handlers.Handle(new ChangeBpmMultiplier(0, BpmMultiplierOp.Halve, _from));

        _handlers.Handle(new ResetDeckToAnalysis(0, _from));

        Assert.Equal(1.0, _deck1.BpmMultiplier);
        Assert.Equal(1, _beatgrid1.Resets);
        Assert.Equal(0, _beatgrid2.Resets);
    }

    // ---- AdjustBpm, NudgeGridFine ---------------------------------------------------------------

    [Fact]
    public void AdjustBpm_reaches_the_named_decks_beatgrid_with_the_delta()
    {
        _handlers.Handle(new AdjustBpm(1, -0.1, _from));

        Assert.Equal([-0.1], _beatgrid2.BpmAdjustments);
        Assert.Empty(_beatgrid1.BpmAdjustments);
    }

    [Fact]
    public void NudgeGridFine_reaches_the_named_decks_beatgrid_with_the_seconds()
    {
        _handlers.Handle(new NudgeGridFine(0, 0.01, _from));

        Assert.Equal([0.01], _beatgrid1.FineNudges);
        Assert.Empty(_beatgrid2.FineNudges);
    }

    // ---- Tune editor ----------------------------------------------------------------------------

    [Fact]
    public void ToggleTuneEditor_opens_then_closes_that_decks_editor()
    {
        _handlers.Handle(new ToggleTuneEditor(0, _from));
        Assert.True(_deck1.EditOpen);
        Assert.False(_deck2.EditOpen);

        _handlers.Handle(new ToggleTuneEditor(0, _from));
        Assert.False(_deck1.EditOpen);
    }

    [Fact]
    public void CloseTuneEditor_closes_an_open_editor()
    {
        _handlers.Handle(new ToggleTuneEditor(1, _from));
        Assert.True(_deck2.EditOpen);

        _handlers.Handle(new CloseTuneEditor(1, _from));

        Assert.False(_deck2.EditOpen);
    }

    [Fact]
    public void CloseTuneEditor_on_a_closed_editor_leaves_it_closed()
    {
        _handlers.Handle(new CloseTuneEditor(0, _from));

        Assert.False(_deck1.EditOpen);
    }

    // ---- ClickGrid ------------------------------------------------------------------------------

    [Fact]
    public void ClickGrid_is_a_no_op_unless_grid_edit_is_active()
    {
        _deck1.ToggleEdit();

        _handlers.Handle(new ClickGrid(0, 5.0, _from));

        Assert.True(_deck1.EditOpen);
        Assert.False(_deck1.GridEditActive);
        Assert.Empty(_beatgrid1.TwoPointGrids);
    }

    [Fact]
    public void Two_clicks_in_grid_edit_set_the_grid_from_the_two_points_and_leave_the_mode()
    {
        _deck1.ToggleGridEdit();

        _handlers.Handle(new ClickGrid(0, 5.0, _from));
        Assert.Empty(_beatgrid1.TwoPointGrids);   // the first click only stores the anchor
        Assert.True(_deck1.GridEditActive);

        _handlers.Handle(new ClickGrid(0, 9.0, _from));

        Assert.Equal([(5.0, 9.0)], _beatgrid1.TwoPointGrids);
        Assert.False(_deck1.GridEditActive);
    }

    [Fact]
    public void Leaving_grid_edit_discards_a_pending_first_click()
    {
        _deck1.ToggleGridEdit();
        _handlers.Handle(new ClickGrid(0, 5.0, _from));
        _deck1.ToggleGridEdit();
        _deck1.ToggleGridEdit();

        _handlers.Handle(new ClickGrid(0, 9.0, _from));
        Assert.Empty(_beatgrid1.TwoPointGrids);   // 9.0 is a fresh first click, not the second of 5.0

        _handlers.Handle(new ClickGrid(0, 12.0, _from));

        Assert.Equal([(9.0, 12.0)], _beatgrid1.TwoPointGrids);
        Assert.False(_deck1.GridEditActive);
    }

    [Fact]
    public void Opening_an_open_editor_raises_nothing()
    {
        var changes = new List<DeckChange>();
        _deck1.Changed += changes.Add;

        _deck1.OpenEdit();
        _deck1.OpenEdit();

        Assert.Equal([DeckChange.EditOpen], changes);
    }

    // ---- Through the bus ------------------------------------------------------------------------

    [Fact]
    public void A_command_sent_on_the_bus_reaches_the_handler()
    {
        var bus = new DataBus(new ThrowingFailureSink());
        bus.Register<ToggleTuneEditor>(_handlers);
        bus.Register<ChangeBpmMultiplier>(_handlers);

        bus.Send(new ToggleTuneEditor(1, _from));
        bus.Send(new ChangeBpmMultiplier(1, BpmMultiplierOp.Double, _from));

        Assert.True(_deck2.EditOpen);
        Assert.Equal(2.0, _deck2.BpmMultiplier);
    }
}

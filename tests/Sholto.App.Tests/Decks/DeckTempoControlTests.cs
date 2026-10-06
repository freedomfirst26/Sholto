using Sholto.App.Analysis.Analyzers;
using Sholto.Data;
using Sholto.App.Decks;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>One deck's tempo control on its own: the BPM multiplier and what it asks the owner to persist,
/// the magnet flag, and the range stops.</summary>
public class DeckTempoControlTests
{
    private static BasicAnalysis MakeBasic(double bpm) => new(
        new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000),
        Bpm: bpm,
        BeatTimes: [],
        DownbeatTimes: []);

    private static (DeckTempoControl Control, ScriptedPorts Ports, List<double> Chosen, List<DeckChange> Changes) Build(double? bpm)
    {
        var ports = new ScriptedPorts(new TestDeckFactory().Create());
        if (bpm is { } b) ports.Loading.Analysis.Set(MakeBasic(b));
        var control = new DeckTempoControl(ports.Loading, ports.Tempo);
        var chosen = new List<double>();
        var changes = new List<DeckChange>();
        control.BpmMultiplierChosen += chosen.Add;
        control.Changed += changes.Add;
        return (control, ports, chosen, changes);
    }

    [Fact]
    public void Toggling_at_120_halves_it_and_toggling_again_returns_to_unity_each_asking_to_persist()
    {
        var (control, ports, chosen, _) = Build(120);

        control.ToggleBpmOverride();
        Assert.Equal(0.5, control.BpmMultiplier);
        Assert.Equal(0.5, ports.Tempo.BpmMultiplier);

        control.ToggleBpmOverride();
        Assert.Equal(1.0, control.BpmMultiplier);
        Assert.Equal([0.5, 1.0], chosen);
    }

    [Fact]
    public void Toggling_below_120_doubles_it()
    {
        var (control, _, chosen, _) = Build(100);

        control.ToggleBpmOverride();

        Assert.Equal(2.0, control.BpmMultiplier);
        Assert.Equal([2.0], chosen);
    }

    [Fact]
    public void Halving_doubling_and_resetting_each_raise_BpmMultiplierChosen()
    {
        var (control, _, chosen, _) = Build(120);

        control.HalveBpm();
        control.DoubleBpm();
        control.ResetBpmMultiplier();

        Assert.Equal([0.5, 1.0, 1.0], chosen);
    }

    [Fact]
    public void Resetting_an_unchanged_multiplier_asks_to_persist_but_raises_no_change()
    {
        var (control, _, chosen, changes) = Build(120);

        control.ResetBpmMultiplier();

        Assert.Equal([1.0], chosen);
        Assert.DoesNotContain(DeckChange.BpmMultiplier, changes);
    }

    [Fact]
    public void Restoring_a_multiplier_pushes_it_and_raises_the_change_but_does_not_ask_to_persist()
    {
        var (control, ports, chosen, changes) = Build(120);

        control.RestoreMultiplier(2.0);

        Assert.Equal(2.0, control.BpmMultiplier);
        Assert.Equal(2.0, ports.Tempo.BpmMultiplier);
        Assert.Equal([DeckChange.BpmMultiplier], changes);
        Assert.Empty(chosen);
    }

    [Fact]
    public void A_magnet_match_sets_the_flag_and_clearing_it_raises_once()
    {
        var (control, ports, _, changes) = Build(128);
        control.SetTempoRange(0.16);

        Assert.True(control.MatchEffectiveBpm(130.0));
        Assert.True(control.WasMagnetAdjusted);
        Assert.Equal(130.0, control.EffectiveBpm, 2);

        changes.Clear();
        control.ClearMagnet();
        control.ClearMagnet();

        Assert.False(control.WasMagnetAdjusted);
        Assert.Equal([DeckChange.MagnetAdjusted], changes);
    }

    [Fact]
    public void Moving_the_fader_clears_the_magnet_flag()
    {
        var (control, _, _, _) = Build(128);
        control.SetTempoRange(0.16);
        control.MatchEffectiveBpm(130.0);

        control.SetTempoPosition(0.5);

        Assert.False(control.WasMagnetAdjusted);
    }

    [Fact]
    public void The_range_cycles_6_10_16_wide_and_back()
    {
        var (control, ports, _, _) = Build(null);
        control.SetTempoRange(0.06);

        var seen = new List<double>();
        for (var i = 0; i < 4; i++)
        {
            control.CycleTempoRange();
            seen.Add(ports.Tempo.TempoRange);
        }

        Assert.Equal([0.10, 0.16, 1.00, 0.06], seen);
    }
}

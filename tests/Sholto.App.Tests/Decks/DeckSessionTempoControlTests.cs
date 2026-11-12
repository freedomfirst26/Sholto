using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;
using Xunit;

namespace Sholto.App.Tests;

/// <summary>Tempo range, magnet adjustment and BPM multiplier handling of the deck session.</summary>
public class DeckSessionTempoControlTests
{
    private static readonly Track SomeTrack = new("/music/a.mp3", "A", "Artist", TimeSpan.FromMinutes(3));

    private static BasicAnalysis MakeBasic() => new(
        new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000),
        Bpm: 128.0,
        BeatTimes: [],
        DownbeatTimes: []);


    /// fires against it.</summary>
    private static ScriptedPorts Ports(bool analysed)
    {
        var ports = new ScriptedPorts(new TestDeckFactory().Create());
        if (analysed) ports.Loading.Analysis.Set(MakeBasic());
        return ports;
    }

    // ---- Tempo ----------------------------------------------------------------------------------

    [Fact]
    public void Matching_the_effective_bpm_flags_a_magnet_adjustment_until_the_user_moves_the_fader()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        rig.Session.SetTempoRange(0.16);

        var changed = rig.Session.MatchEffectiveBpm(130.0);

        Assert.True(changed);
        Assert.True(rig.Session.WasMagnetAdjusted);
        Assert.Equal(130.0, rig.Session.EffectiveBpm, 2);

        rig.Session.SetTempoPosition(0.5);

        Assert.False(rig.Session.WasMagnetAdjusted);
    }

    [Fact]
    public void Matching_the_effective_bpm_does_nothing_without_analysis()
    {
        var rig = new DeckSessionRig(Ports(analysed: false));

        Assert.False(rig.Session.MatchEffectiveBpm(130.0));
        Assert.False(rig.Session.WasMagnetAdjusted);
    }

    [Fact]
    public void The_tempo_range_cycles_6_10_16_wide_and_back()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());
        rig.Session.SetTempoRange(0.06);

        var seen = new List<double>();
        for (var i = 0; i < 4; i++)
        {
            rig.Session.CycleTempoRange();
            seen.Add(rig.Ports.Tempo.TempoRange);
        }

        Assert.Equal([0.10, 0.16, 1.00, 0.06], seen);
    }

    // ---- BPM multiplier -------------------------------------------------------------------------

    [Fact]
    public void Halving_the_bpm_asks_the_owner_to_persist_it_but_a_multiplier_restored_by_a_load_does_not()
    {
        var ports = Ports(analysed: true);
        var rig = new DeckSessionRig(ports);
        var chosen = new List<(string Path, double Multiplier)>();
        rig.Session.BpmMultiplierChosen += (path, multiplier) => chosen.Add((path, multiplier));

        rig.Session.LoadTrack(SomeTrack, SomeTrack.FilePath, [], bpmMultiplier: 2.0);
        Assert.Equal(2.0, rig.Session.BpmMultiplier);
        Assert.Empty(chosen);

        rig.Session.HalveBpm();

        Assert.Equal(1.0, rig.Session.BpmMultiplier);
        Assert.Equal([(SomeTrack.FilePath, 1.0)], chosen);
    }

    [Fact]
    public void Beginning_a_load_clears_a_magnet_adjustment()
    {
        var rig = new DeckSessionRig(Ports(analysed: true));
        rig.Session.SetTempoRange(0.16);
        Assert.True(rig.Session.MatchEffectiveBpm(130.0));
        Assert.True(rig.Session.WasMagnetAdjusted);

        rig.Session.BeginLoad(SomeTrack);

        Assert.False(rig.Session.WasMagnetAdjusted);
    }

    [Fact]
    public void Unloading_clears_a_magnet_adjustment()
    {
        var rig = new DeckSessionRig(Ports(analysed: true));
        rig.Session.SetTempoRange(0.16);
        Assert.True(rig.Session.MatchEffectiveBpm(130.0));

        rig.Session.Unload();

        Assert.False(rig.Session.WasMagnetAdjusted);
    }

    [Fact]
    public void Beginning_or_streaming_a_load_restores_the_multiplier_to_the_audio_without_asking_to_persist_it()
    {
        var rig = new DeckSessionRig(Ports(analysed: true));
        var chosen = new List<(string Path, double Multiplier)>();
        rig.Session.BpmMultiplierChosen += (path, multiplier) => chosen.Add((path, multiplier));

        rig.Session.BeginLoad(SomeTrack, 2.0);
        Assert.Equal(2.0, rig.Session.BpmMultiplier);
        Assert.Equal(2.0, rig.Ports.Tempo.BpmMultiplier);

        rig.Session.LoadStreaming(SomeTrack, SomeTrack.FilePath, 0.5);
        Assert.Equal(0.5, rig.Session.BpmMultiplier);
        Assert.Equal(0.5, rig.Ports.Tempo.BpmMultiplier);

        Assert.Empty(chosen);
    }

    [Fact]
    public void Resetting_an_unchanged_multiplier_still_asks_the_owner_to_persist_it_but_raises_no_change()
    {
        // Pins current behaviour: the persist request is unconditional, the change event is not.
        var rig = new DeckSessionRig(Ports(analysed: true));
        rig.Session.LoadTrack(SomeTrack, SomeTrack.FilePath, [], bpmMultiplier: 1.0);
        var chosen = new List<(string Path, double Multiplier)>();
        rig.Session.BpmMultiplierChosen += (path, multiplier) => chosen.Add((path, multiplier));
        var changes = new List<DeckChange>();
        rig.Session.Changed += changes.Add;

        rig.Session.ResetBpmMultiplier();

        Assert.Equal([(SomeTrack.FilePath, 1.0)], chosen);
        Assert.DoesNotContain(DeckChange.BpmMultiplier, changes);
    }

    [Fact]
    public void Halving_with_no_track_loaded_changes_the_multiplier_but_asks_nothing()
    {
        var rig = new DeckSessionRig(Ports(analysed: true));
        var chosen = new List<(string Path, double Multiplier)>();
        rig.Session.BpmMultiplierChosen += (path, multiplier) => chosen.Add((path, multiplier));

        rig.Session.HalveBpm();

        Assert.Equal(0.5, rig.Session.BpmMultiplier);
        Assert.Empty(chosen);
    }

    [Fact]
    public void Matching_a_bpm_already_within_a_hundredth_changes_nothing()
    {
        var rig = new DeckSessionRig(Ports(analysed: true));
        rig.Session.SetTempoRange(0.16);
        var positionBefore = rig.Ports.Tempo.TempoPosition;

        Assert.False(rig.Session.MatchEffectiveBpm(128.005));

        Assert.False(rig.Session.WasMagnetAdjusted);
        Assert.Equal(positionBefore, rig.Ports.Tempo.TempoPosition);
    }

    [Fact]
    public void Cycling_from_a_range_between_stops_goes_to_ten_percent()
    {
        var rig = new DeckSessionRig(new TestDeckFactory().Create());
        rig.Session.SetTempoRange(0.08);

        rig.Session.CycleTempoRange();

        Assert.Equal(0.10, rig.Ports.Tempo.TempoRange);
    }
}

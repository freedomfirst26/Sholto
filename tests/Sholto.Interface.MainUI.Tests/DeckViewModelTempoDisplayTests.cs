using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The BPM and tempo-range displays projected from the tempo event.</summary>
[Collection(DeckViewModelCollection.Name)]
public class DeckViewModelTempoDisplayTests
{
    private readonly DeckViewModelRig rig = new();

    [Fact]
    public void Tempo_shows_the_effective_and_original_bpm_and_the_range()
    {
        rig.Bus.Publish(new DeckTempoChanged(0, 128.0, 0.5, 64.0, 1.0, 0.06, true, false));

        Assert.Equal(128.0, rig.Deck.SourceBpm);
        Assert.Equal(0.5, rig.Deck.BpmMultiplier);
        Assert.Equal(64.0, rig.Deck.EffectiveBpm);
        Assert.True(rig.Deck.HasBpm);
        Assert.Equal("64.0 BPM", rig.Deck.BpmDisplay);
        Assert.Equal("64.0", rig.Deck.BpmDisplayShort);
        Assert.Equal("64.0", rig.Deck.OriginalBpmShort);
        Assert.True(rig.Deck.ShowOriginalBpm);
        Assert.Equal("±6%", rig.Deck.TempoRangeDisplay);
    }

    [Fact]
    public void The_widest_tempo_range_reads_WIDE()
    {
        rig.Bus.Publish(new DeckTempoChanged(0, 128.0, 1.0, 128.0, 1.0, 1.0, false, false));

        Assert.Equal("WIDE", rig.Deck.TempoRangeDisplay);
    }

    [Fact]
    public void The_original_bpm_chip_shows_for_a_shifted_fader_or_a_magnet_adjustment_only()
    {
        rig.Bus.Publish(new DeckTempoChanged(0, 128.0, 1.0, 128.0, 1.0, 0.06, false, false));
        Assert.False(rig.Deck.ShowOriginalBpm);

        rig.Bus.Publish(new DeckTempoChanged(0, 128.0, 1.0, 128.1, 1.0, 0.06, false, true));
        Assert.True(rig.Deck.ShowOriginalBpm);
        Assert.True(rig.Deck.WasMagnetAdjusted);
    }

    [Fact]
    public void Without_a_source_bpm_the_displays_are_empty()
    {
        rig.Bus.Publish(new DeckTempoChanged(0, 0.0, 1.0, 0.0, 1.0, 0.06, false, false));

        Assert.False(rig.Deck.HasBpm);
        Assert.Equal("", rig.Deck.BpmDisplay);
        Assert.Equal("", rig.Deck.BpmDisplayShort);
        Assert.Equal("", rig.Deck.OriginalBpmShort);
    }

    [Fact]
    public void Tempo_for_the_other_deck_is_ignored()
    {
        rig.Bus.Publish(new DeckTempoChanged(1, 128.0, 1.0, 128.0, 1.0, 0.06, false, false));

        Assert.Equal(0.0, rig.Deck.SourceBpm);
        Assert.Empty(rig.Changed);
    }
}

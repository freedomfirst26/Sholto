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

/// <summary>Stem chips: level maps to opacity, mute decides active, and only the chip that moved is notified.</summary>
[Collection(DeckViewModelCollection.Name)]
public class DeckViewModelChipLevelsTests
{
    private readonly DeckViewModelRig rig = new();

    [Fact]
    public void Every_stem_chip_starts_at_full_opacity()
    {
        Assert.Equal(1.0, rig.Deck.DrumsChipOpacity);
        Assert.Equal(1.0, rig.Deck.VocalsChipOpacity);
        Assert.Equal(1.0, rig.Deck.InstrumentalChipOpacity);
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.5, 0.65)]
    [InlineData(0.0, 0.3)]
    [InlineData(0.75, 0.825)]
    [InlineData(1.5, 1.0)]
    public void A_stem_level_maps_to_chip_opacity_from_0_3_up_to_1(double level, double opacity)
    {
        rig.Bus.Publish(new StemLevelChanged(0, 0, level));
        rig.Bus.Publish(new StemLevelChanged(0, 1, level));
        rig.Bus.Publish(new StemLevelChanged(0, 2, level));

        Assert.Equal(opacity, rig.Deck.DrumsChipOpacity, 6);
        Assert.Equal(opacity, rig.Deck.VocalsChipOpacity, 6);
        Assert.Equal(opacity, rig.Deck.InstrumentalChipOpacity, 6);
    }

    [Fact]
    public void A_muted_stem_stays_hollow_at_full_opacity_whatever_its_level()
    {
        rig.Bus.Publish(new StemLevelChanged(0, 0, 0.2));
        rig.Bus.Publish(new StemMuteChanged(0, 0, true));

        Assert.False(rig.Deck.DrumsActive);
        Assert.Equal(1.0, rig.Deck.DrumsChipOpacity);

        rig.Bus.Publish(new StemMuteChanged(0, 0, false));

        Assert.True(rig.Deck.DrumsActive);
        Assert.True(rig.Deck.DrumsChipOpacity < 1.0);
    }

    [Fact]
    public void A_stem_level_notifies_only_that_chip_and_only_when_its_opacity_changes()
    {
        rig.Changed.Clear();

        rig.Bus.Publish(new StemLevelChanged(0, 1, 0.5));
        rig.Bus.Publish(new StemLevelChanged(0, 1, 0.5));
        rig.Bus.Publish(new StemLevelChanged(0, 2, 1.2));
        rig.Bus.Publish(new StemLevelChanged(1, 0, 0.1));

        Assert.Equal([nameof(DeckViewModel.VocalsChipOpacity)], rig.Changed);
    }

    [Fact]
    public void Every_stem_starts_active()
    {
        Assert.True(rig.Deck.DrumsActive);
        Assert.True(rig.Deck.VocalsActive);
        Assert.True(rig.Deck.InstrumentalActive);
    }

    [Fact]
    public void A_muted_stem_is_not_active_and_unmuting_makes_it_active_again()
    {
        rig.Bus.Publish(new StemMuteChanged(0, 0, true));
        rig.Bus.Publish(new StemMuteChanged(0, 1, true));
        rig.Bus.Publish(new StemMuteChanged(0, 2, true));

        Assert.False(rig.Deck.DrumsActive);
        Assert.False(rig.Deck.VocalsActive);
        Assert.False(rig.Deck.InstrumentalActive);

        rig.Bus.Publish(new StemMuteChanged(0, 1, false));

        Assert.False(rig.Deck.DrumsActive);
        Assert.True(rig.Deck.VocalsActive);
        Assert.False(rig.Deck.InstrumentalActive);
    }

    [Fact]
    public void A_stem_mute_for_the_other_deck_is_ignored()
    {
        rig.Bus.Publish(new StemMuteChanged(1, 0, true));

        Assert.True(rig.Deck.DrumsActive);
        Assert.Empty(rig.Changed);
    }
}

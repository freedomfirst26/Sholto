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

/// <summary>The analysis-derived display (key, stems, vocal regions, can-play) projected from the content event.</summary>
[Collection(DeckViewModelCollection.Name)]
public class DeckViewModelAnalysisDisplayTests
{
    private readonly DeckViewModelRig rig = new();

    [Fact]
    public void A_loaded_track_with_analysis_can_play_and_shows_the_info_at_full_opacity()
    {
        var analysis = rig.AnalysisWithBasic();

        rig.Bus.Publish(rig.Content(0, DeckLoadState.Loaded, true, analysis, rig.SomeTrack));

        Assert.Same(rig.SomeTrack, rig.Deck.LoadedTrack);
        Assert.Equal(DeckLoadState.Loaded, rig.Deck.LoadState);
        Assert.Equal(1.0, rig.Deck.InfoOpacity);
        Assert.True(rig.Deck.IsLoaded);
        Assert.True(rig.Deck.HasAnalysis);
        Assert.True(rig.Deck.CanPlay);
        Assert.Same(analysis, rig.Deck.Analysis);
        Assert.Same(analysis.Peaks, rig.Deck.Peaks);
        Assert.Contains(nameof(DeckViewModel.LoadedTrack), rig.Changed);
        Assert.Contains(nameof(DeckViewModel.LoadState), rig.Changed);
        Assert.Contains(nameof(DeckViewModel.InfoOpacity), rig.Changed);
        Assert.Contains(nameof(DeckViewModel.IsLoaded), rig.Changed);
        Assert.Contains(nameof(DeckViewModel.CanPlay), rig.Changed);
    }

    [Fact]
    public void A_landed_key_shows_its_Camelot_code_and_notifies_it()
    {
        rig.Bus.Publish(rig.Content(0, DeckLoadState.Loaded, true, rig.AnalysisWithBasic() with { Key = new KeyRef(0, true) }, rig.SomeTrack));

        Assert.Equal("8B", rig.Deck.Camelot);
        Assert.True(rig.Deck.HasKey);
        Assert.Contains(nameof(DeckViewModel.Camelot), rig.Changed);
    }

    [Fact]
    public void Landed_stems_show_the_stem_chips()
    {
        Assert.False(rig.Deck.HasStems);

        rig.Bus.Publish(rig.Content(0, DeckLoadState.Loaded, true, rig.AnalysisWithBasic() with { HasStems = true }, rig.SomeTrack));

        Assert.True(rig.Deck.HasStems);
        Assert.Contains(nameof(DeckViewModel.HasStems), rig.Changed);
    }

    [Fact]
    public void Landed_vocal_regions_reach_VocalRegions()
    {
        Assert.Null(rig.Deck.VocalRegions);
        VocalRegion[] regions = [new VocalRegion(1, 2)];

        rig.Bus.Publish(rig.Content(0, DeckLoadState.Loaded, true, rig.AnalysisWithBasic() with { VocalRegions = regions }, rig.SomeTrack));

        Assert.Same(regions, rig.Deck.VocalRegions);
        Assert.Contains(nameof(DeckViewModel.VocalRegions), rig.Changed);
    }

    [Fact]
    public void A_loaded_track_whose_analysis_has_not_landed_cannot_play()
    {
        rig.Bus.Publish(rig.Content(0, DeckLoadState.Loaded, true, rig.EmptyAnalysis(), rig.SomeTrack));

        Assert.True(rig.Deck.IsLoaded);
        Assert.False(rig.Deck.HasAnalysis);
        Assert.False(rig.Deck.CanPlay);
    }

    [Fact]
    public void An_idle_deck_with_no_track_shows_nothing_loaded()
    {
        rig.Bus.Publish(rig.Content(0, DeckLoadState.Loaded, true, rig.AnalysisWithBasic(), rig.SomeTrack));

        rig.Bus.Publish(rig.Content(0, DeckLoadState.Idle, false));

        Assert.Null(rig.Deck.LoadedTrack);
        Assert.False(rig.Deck.IsLoaded);
        Assert.False(rig.Deck.HasAnalysis);
        Assert.False(rig.Deck.CanPlay);
        Assert.Equal(0.3, rig.Deck.InfoOpacity);
    }
}

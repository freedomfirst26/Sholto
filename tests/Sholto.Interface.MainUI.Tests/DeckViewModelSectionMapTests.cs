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

/// <summary>The song-section map projected from the sections event.</summary>
[Collection(DeckViewModelCollection.Name)]
public class DeckViewModelSectionMapTests
{
    private readonly DeckViewModelRig rig = new();

    [Fact]
    public void Sections_event_is_projected_and_ignored_for_the_other_deck()
    {
        var sections = new List<DeckSection> { new(DeckSectionKind.Intro, 0, 8), new(DeckSectionKind.Drop, 8, 16) };

        rig.Bus.Publish(new DeckSectionsChanged(1, [], new DeckPhraseGrid(0, 8), 0, 0, 0));
        Assert.Empty(rig.Deck.Sections);

        rig.Bus.Publish(new DeckSectionsChanged(0, sections, new DeckPhraseGrid(4, 8), 0.25, 1.875, 24));

        Assert.Equal(sections, rig.Deck.Sections);
        Assert.Equal(4, rig.Deck.PhraseGrid.PhaseBar);
        Assert.Equal(0.25, rig.Deck.FirstDownbeatSec);
        Assert.Equal(1.875, rig.Deck.BarPeriodSec);
        Assert.Equal(24, rig.Deck.TotalBars);
        Assert.Contains(nameof(DeckViewModel.Sections), rig.Changed);
        Assert.True(rig.Deck.HasSections);
        Assert.Contains(nameof(DeckViewModel.SectionMapVisible), rig.Changed);
    }

    [Fact]
    public void Sections_without_a_bar_grid_do_not_count_as_sections()
    {
        rig.Bus.Publish(new DeckSectionsChanged(0, [new(DeckSectionKind.Intro, 0, 8)], new DeckPhraseGrid(0, 8), 0, 0, 0));
        Assert.False(rig.Deck.HasSections);
    }
}

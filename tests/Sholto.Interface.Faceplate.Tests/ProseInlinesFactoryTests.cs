using Avalonia.Controls.Documents;
using Sholto.Interface.Faceplate.Devices.DdjFlx4;
using Sholto.Interface.Faceplate.Model;
using Sholto.Interface.Faceplate.Views;
using Xunit;

namespace Sholto.Interface.Faceplate.Tests;

public class ProseInlinesFactoryTests
{
    private readonly ProseInlinesFactory _prose = new();
    private readonly ComboMarkerFormatter _comboMarkers = new();

    private FaceplateDoc Doc() => new FaceplateDocLoader().Load(new DdjFlx4Faceplate());

    [Fact]
    public void A_plain_string_with_no_markers_becomes_a_single_run()
    {
        var inlines = _prose.Create("Starts playback if the deck is paused.", Doc(), deck: 0);

        Assert.Equal("Starts playback if the deck is paused.", inlines.Text);
        Assert.DoesNotContain(inlines, i => i is InlineUIContainer);
    }

    [Fact]
    public void One_marker_becomes_a_chip_with_the_surrounding_text_kept_as_plain_runs()
    {
        var inlines = _prose.Create("Hold [[deck.shift]] to unlock it.", Doc(), deck: 0);

        var containers = inlines.OfType<InlineUIContainer>().ToList();
        Assert.Single(containers);
        var chip = Assert.IsType<ControlChip>(containers[0].Child);
        Assert.Equal("deck.shift", chip.ControlId);
        Assert.Equal("SHIFT", chip.Label);
        // deck.shift is per-deck, so the chip inherits the deck passed to Build.
        Assert.Equal(0, chip.Deck);

        // The surrounding words survive untouched, as plain runs either side of the chip.
        var runText = string.Concat(inlines.OfType<Run>().Select(r => r.Text));
        Assert.Equal("Hold  to unlock it.", runText);
    }

    [Fact]
    public void Several_markers_each_become_their_own_chip()
    {
        var text = "While [[fx.onoff]] is held, use [[mixer.eq.hi]], [[mixer.eq.mid]] and [[mixer.eq.low]].";
        var inlines = _prose.Create(text, Doc(), deck: 1);

        var chips = inlines.OfType<InlineUIContainer>().Select(c => (ControlChip)c.Child!).ToList();
        Assert.Equal(4, chips.Count);
        Assert.Equal(["fx.onoff", "mixer.eq.hi", "mixer.eq.mid", "mixer.eq.low"], chips.Select(c => c.ControlId));
        // fx.onoff is global -> deck -1; the three EQ knobs are per-deck -> inherit deck 1.
        Assert.Equal([-1, 1, 1, 1], chips.Select(c => c.Deck));
    }

    [Fact]
    public void An_unknown_id_renders_as_its_own_raw_text_instead_of_throwing()
    {
        var inlines = _prose.Create("Press [[not.a.real.control]] to do it.", Doc(), deck: 0);

        Assert.DoesNotContain(inlines, i => i is InlineUIContainer);
        Assert.Equal("Press [[not.a.real.control]] to do it.", inlines.Text);
    }

    [Fact]
    public void A_malformed_marker_with_no_closing_bracket_is_left_as_plain_text()
    {
        var inlines = _prose.Create("Hold [[deck.shift and turn.", Doc(), deck: 0);

        Assert.DoesNotContain(inlines, i => i is InlineUIContainer);
        Assert.Equal("Hold [[deck.shift and turn.", inlines.Text);
    }

    [Fact]
    public void Format_builds_from_ids_not_by_splitting_a_rendered_string()
    {
        var markers = _comboMarkers.Format(["deck.shift"], "deck.jog");
        Assert.Equal("[[deck.shift]] + [[deck.jog]]", markers);

        var inlines = _prose.Create(markers, Doc(), deck: 0);
        var chips = inlines.OfType<InlineUIContainer>().Select(c => (ControlChip)c.Child!).ToList();
        Assert.Equal(["deck.shift", "deck.jog"], chips.Select(c => c.ControlId));
        var runText = string.Concat(inlines.OfType<Run>().Select(r => r.Text));
        Assert.Equal(" + ", runText);
    }
}

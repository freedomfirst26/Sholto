using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Minimap;

namespace Sholto.Interface.MainUI.Tests.Minimap;

public class MinimapSectionLabelsTests
{
    private readonly MinimapSectionLabels _labels = new();

    // One unit per character, so "DROP 16" is 7 wide and "DROP" is 4.
    private static float Chars(string t) => t.Length;

    [Fact]
    public void A_label_reads_the_kind_then_the_bar_count()
    {
        Assert.Equal("DROP 16", _labels.Fit(DeckSectionKind.Drop, 16, 100, Chars));
        Assert.Equal("BUILD 8", _labels.Fit(DeckSectionKind.Build, 8, 100, Chars));
        Assert.Equal("BREAK 32", _labels.Fit(DeckSectionKind.Breakdown, 32, 100, Chars));
    }

    [Fact]
    public void A_narrow_block_drops_the_bar_count_then_the_whole_label()
    {
        Assert.Equal("DROP 16", _labels.Fit(DeckSectionKind.Drop, 16, 7, Chars));
        Assert.Equal("DROP", _labels.Fit(DeckSectionKind.Drop, 16, 6.9f, Chars));
        Assert.Equal("DROP", _labels.Fit(DeckSectionKind.Drop, 16, 4, Chars));
        Assert.Equal("", _labels.Fit(DeckSectionKind.Drop, 16, 3.9f, Chars));
    }

    [Fact]
    public void A_neutral_section_has_no_label()
    {
        Assert.Equal("", _labels.Fit(DeckSectionKind.Section, 16, 100, Chars));
    }
}

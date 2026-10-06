using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Minimap;

namespace Sholto.Interface.MainUI.Tests.Minimap;

public class MinimapPhraseLinesTests
{
    private readonly MinimapPhraseLines _lines = new();

    private static (int Bar, MinimapPhraseLineWeight Weight)[] Of(IReadOnlyList<MinimapPhraseLine> l) =>
        l.Select(x => (x.Bar, x.Weight)).ToArray();

    private const MinimapPhraseLineWeight T = MinimapPhraseLineWeight.Thin;
    private const MinimapPhraseLineWeight M = MinimapPhraseLineWeight.Medium;
    private const MinimapPhraseLineWeight S = MinimapPhraseLineWeight.Strong;

    [Fact]
    public void Phase_0_is_thin_every_8_bars_medium_every_16_and_strong_every_32()
    {
        Assert.Equal(
            [(0, S), (8, T), (16, M), (24, T), (32, S), (40, T), (48, M)],
            Of(_lines.Lines(new DeckPhraseGrid(0, 8), 50)));
    }

    [Fact]
    public void Phase_1_counts_from_bar_1()
    {
        Assert.Equal(
            [(1, S), (9, T), (17, M), (25, T), (33, S), (41, T)],
            Of(_lines.Lines(new DeckPhraseGrid(1, 8), 45)));
    }

    [Fact]
    public void Phase_4_counts_from_bar_4()
    {
        Assert.Equal(
            [(4, S), (12, T), (20, M), (28, T), (36, S)],
            Of(_lines.Lines(new DeckPhraseGrid(4, 8), 39)));
    }

    [Fact]
    public void A_phase_beyond_the_first_phrase_still_lines_up_bars_before_it()
    {
        // Phase 17: bar 17 is strong, so bar 1 is one phrase-pair back: 17 - 16 = 1 is medium, 9 is thin.
        Assert.Equal(
            [(1, M), (9, T), (17, S), (25, T)],
            Of(_lines.Lines(new DeckPhraseGrid(17, 8), 30)));
    }

    [Fact]
    public void No_phrase_length_or_no_bars_gives_no_lines()
    {
        Assert.Empty(_lines.Lines(new DeckPhraseGrid(0, 0), 50));
        Assert.Empty(_lines.Lines(new DeckPhraseGrid(0, 8), -1));
    }
}

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
            Of(_lines.Lines(new DeckPhraseGrid(0, 8), 50, 10)));
    }

    [Fact]
    public void Phase_1_counts_from_bar_1()
    {
        Assert.Equal(
            [(1, S), (9, T), (17, M), (25, T), (33, S), (41, T)],
            Of(_lines.Lines(new DeckPhraseGrid(1, 8), 45, 10)));
    }

    [Fact]
    public void Phase_4_counts_from_bar_4()
    {
        Assert.Equal(
            [(4, S), (12, T), (20, M), (28, T), (36, S)],
            Of(_lines.Lines(new DeckPhraseGrid(4, 8), 39, 10)));
    }

    [Fact]
    public void A_phase_beyond_the_first_phrase_still_lines_up_bars_before_it()
    {
        // Phase 17: bar 17 is strong, so bar 1 is one phrase-pair back: 17 - 16 = 1 is medium, 9 is thin.
        Assert.Equal(
            [(1, M), (9, T), (17, S), (25, T)],
            Of(_lines.Lines(new DeckPhraseGrid(17, 8), 30, 10)));
    }

    [Fact]
    public void Thin_lines_drop_below_6_dip_spacing()
    {
        // 8 bars * 0.5 = 4 dips between thin lines: too tight, so only the 16- and 32-bar lines stay.
        Assert.Equal(
            [(0, S), (16, M), (32, S), (48, M), (64, S)],
            Of(_lines.Lines(new DeckPhraseGrid(0, 8), 64, 0.5)));
    }

    [Fact]
    public void Medium_lines_drop_below_6_dip_spacing()
    {
        // 16 bars * 0.3 = 4.8 dips between medium lines: too tight, so only the 32-bar lines stay.
        Assert.Equal(
            [(0, S), (32, S), (64, S)],
            Of(_lines.Lines(new DeckPhraseGrid(0, 8), 64, 0.3)));
    }

    [Fact]
    public void No_phrase_length_or_no_bars_gives_no_lines()
    {
        Assert.Empty(_lines.Lines(new DeckPhraseGrid(0, 0), 50, 10));
        Assert.Empty(_lines.Lines(new DeckPhraseGrid(0, 8), -1, 10));
    }
}

using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.Minimap;

/// <summary>Which bars carry a phrase gap, and how strong: pure arithmetic, no drawing.</summary>
public interface IMinimapPhraseLines
{
    /// <summary>The gaps from bar 0 to <paramref name="lastBar"/> inclusive, in bar order. Gaps fall every
    /// <c>PhraseBars</c> bars counted from <c>PhaseBar</c> (which is itself a strong gap); counted from
    /// there, every second phrase is medium and every fourth is strong, so bars before the phase line up
    /// with it too. Thin (8-bar) gaps are dropped
    /// when <c>PhraseBars * dipsPerBar</c> is under <see cref="MinimapMetrics.MinPhraseSpacing"/>, medium (16-bar)
    /// gaps when twice that is; strong gaps always stay. Empty when the phrase length is not positive.</summary>
    /// <param name="dipsPerBar">The width of one bar on the minimap, in DIPs.</param>
    IReadOnlyList<MinimapPhraseLine> Lines(DeckPhraseGrid grid, int lastBar, double dipsPerBar);
}

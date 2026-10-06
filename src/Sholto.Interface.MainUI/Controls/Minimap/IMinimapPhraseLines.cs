using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.Minimap;

/// <summary>Which bars carry a phrase line, and how strong: pure arithmetic, no drawing.</summary>
public interface IMinimapPhraseLines
{
    /// <summary>The lines from bar 0 to <paramref name="lastBar"/> inclusive, in bar order. Lines fall every
    /// <c>PhraseBars</c> bars counted from <c>PhaseBar</c> (which is itself a strong line); counted from
    /// there, every second phrase is medium and every fourth is strong, so bars before the phase line up
    /// with it too. Empty when the phrase length is not positive.</summary>
    IReadOnlyList<MinimapPhraseLine> Lines(DeckPhraseGrid grid, int lastBar);
}

using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.Minimap;

public sealed class MinimapPhraseLines : IMinimapPhraseLines
{
    public IReadOnlyList<MinimapPhraseLine> Lines(DeckPhraseGrid grid, int lastBar)
    {
        int unit = grid.PhraseBars;
        if (unit <= 0 || lastBar < 0) return [];

        var lines = new List<MinimapPhraseLine>();
        int first = ((grid.PhaseBar % unit) + unit) % unit;
        for (int bar = first; bar <= lastBar; bar += unit)
        {
            int phrase = (bar - grid.PhaseBar) / unit;
            int step = ((phrase % 4) + 4) % 4;
            var weight = step == 0 ? MinimapPhraseLineWeight.Strong
                : step == 2 ? MinimapPhraseLineWeight.Medium
                : MinimapPhraseLineWeight.Thin;
            lines.Add(new MinimapPhraseLine(bar, weight));
        }
        return lines;
    }
}

namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>Where phrases fall on the bar grid. Phrase lines recur every
/// <paramref name="PhraseBars"/> bars from <paramref name="PhaseBar"/>.</summary>
/// <param name="PhaseBar">Bar offset (0-7) of the first phrase line. Real tracks start at 0, 1 or 4.</param>
/// <param name="PhraseBars">Bars per phrase; 8 for dance music.</param>
public sealed record PhraseGrid(int PhaseBar, int PhraseBars = 8)
{
    /// <summary>True when <paramref name="bar"/> starts a phrase.</summary>
    public bool IsPhraseLine(int bar) => PositiveMod(bar - PhaseBar, PhraseBars) == 0;

    /// <summary>Strength of the line at <paramref name="bar"/>, in bars: <see cref="PhraseBars"/>
    /// (8 by default), 2x (16) or 4x (32) counted from <see cref="PhaseBar"/>; 0 when not a phrase line.</summary>
    public int LineWeight(int bar)
    {
        if (!IsPhraseLine(bar)) return 0;
        int rel = bar - PhaseBar;
        if (PositiveMod(rel, PhraseBars * 4) == 0) return PhraseBars * 4;
        if (PositiveMod(rel, PhraseBars * 2) == 0) return PhraseBars * 2;
        return PhraseBars;
    }

    private int PositiveMod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
}

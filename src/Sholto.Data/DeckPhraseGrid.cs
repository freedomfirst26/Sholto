namespace Sholto.Data;

/// <summary>Where phrases fall on the bar grid: a phrase line every <paramref name="PhraseBars"/> bars
/// from <paramref name="PhaseBar"/>.</summary>
public readonly record struct DeckPhraseGrid(int PhaseBar, int PhraseBars);

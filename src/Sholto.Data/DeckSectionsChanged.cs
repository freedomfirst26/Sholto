namespace Sholto.Data;

/// <summary>A deck's phrase-aware section list, its phrase grid and the bar grid that turns bars into time.
/// Published when basic analysis lands (including after a grid nudge or BPM edit, which re-run the
/// analyser) and when the track unloads (empty list). State: a late subscriber is told the current value.
/// <para>Bar to time: <c>seconds(bar) = FirstDownbeatSec + bar * BarPeriodSec</c>. The grid is
/// constant-spacing, so those two numbers are the whole map; <c>BarPeriodSec</c> is 0 when there is no grid.</para></summary>
/// <param name="Deck">Deck index.</param>
/// <param name="Sections">Sections in bar order; empty when the track has no usable grid or is unloaded.</param>
/// <param name="PhraseGrid">Phrase lines; meaningful only when <paramref name="Sections"/> is non-empty.</param>
/// <param name="FirstDownbeatSec">Time of bar 0.</param>
/// <param name="BarPeriodSec">Seconds per bar.</param>
/// <param name="TotalBars">Whole bars in the track (the minimap's bar-axis length).</param>
public readonly record struct DeckSectionsChanged(
    int Deck, IReadOnlyList<DeckSection> Sections, DeckPhraseGrid PhraseGrid,
    double FirstDownbeatSec, double BarPeriodSec, int TotalBars) : IStateEvent
{
    public int Slot => Deck;
}

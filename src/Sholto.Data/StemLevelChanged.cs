namespace Sholto.Data;

/// <summary>One stem of one deck had its level changed (Shift + the EQ knobs). <see cref="Stem"/> is
/// 0 = drums, 1 = vocals, 2 = instrumental. <see cref="Level"/> is the App's gain: 1.0 is unity, 0 is
/// silent, and it can go above 1.</summary>
public readonly record struct StemLevelChanged(int Deck, int Stem, double Level) : IStateEvent
{
    public int Slot => Deck * StemMuteChanged.StemsPerDeck + Stem;
}

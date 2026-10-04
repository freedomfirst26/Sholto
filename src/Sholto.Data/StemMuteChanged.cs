namespace Sholto.Data;

/// <summary>One stem of one deck was muted or unmuted. <see cref="Stem"/> is 0 = drums, 1 = vocals,
/// 2 = instrumental.</summary>
public readonly record struct StemMuteChanged(int Deck, int Stem, bool Muted) : IStateEvent
{
    /// <summary>Stems per deck: the multiplier that makes (deck, stem) a unique slot.</summary>
    public const int StemsPerDeck = 3;

    public int Slot => Deck * StemsPerDeck + Stem;
}

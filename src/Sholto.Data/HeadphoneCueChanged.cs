namespace Sholto.Data;

/// <summary>A deck's headphone cue (pre-fader listen) was switched on or off.</summary>
public readonly record struct HeadphoneCueChanged(int Deck, bool On) : IStateEvent
{
    public int Slot => Deck;
}

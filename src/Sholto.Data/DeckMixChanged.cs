namespace Sholto.Data;

/// <summary>A deck's volume picture: whether its channel fader has been measured, its position, the combined channel x crossfade gain (0 when unmeasured) and whether it is measured and effectively silent. State.</summary>
public readonly record struct DeckMixChanged(int Deck, bool GainKnown, double ChannelGain, double EffectiveGain, bool IsMuted) : IStateEvent
{
    public int Slot => Deck;
}

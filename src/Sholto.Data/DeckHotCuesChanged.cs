namespace Sholto.Data;

/// <summary>The hot cues saved on a deck's loaded track, at most one per pad. Shared by reference; do not modify. State.</summary>
public readonly record struct DeckHotCuesChanged(int Deck, HotCue[] Cues) : IStateEvent
{
    public int Slot => Deck;
}

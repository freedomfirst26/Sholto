using Sholto.App.Decks;

namespace Sholto.App.Performance;

/// <summary>See <see cref="IJogSeek"/>.</summary>
public sealed class JogSeek(IDecks decks, IJogRecencyReader recency) : IJogSeek
{
    private readonly IDecks _decks = decks;
    private readonly IJogRecencyReader _recency = recency;
    private double _pending1, _pending2;

    public void Add(int deck, double seconds)
    {
        if (deck == 0) _pending1 += seconds; else _pending2 += seconds;
    }

    public double PendingSeconds(int deck) => deck == 0 ? _pending1 : _pending2;

    public void Flush(double scale)
    {
        if (_pending1 != 0) { _decks.Deck1.Transport.SeekRelative(_pending1 * scale); _pending1 = 0; }
        if (_pending2 != 0) { _decks.Deck2.Transport.SeekRelative(_pending2 * scale); _pending2 = 0; }
    }

    public void UpdateScrubbing(DateTime now)
    {
        _decks.Deck1.IsScrubbing = _recency.LastJoggedDeck == 1 && (now - _recency.LastJogAt) < _recency.ActiveJogWindow;
        _decks.Deck2.IsScrubbing = _recency.LastJoggedDeck == 2 && (now - _recency.LastJogAt) < _recency.ActiveJogWindow;
    }
}

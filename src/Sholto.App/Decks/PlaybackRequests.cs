namespace Sholto.App.Decks;

/// <summary>See <see cref="IPlaybackRequests"/>. A playing, scratch-capable deck is not paused at once: the
/// request goes to the scratch engine, which runs a vinyl-style brake (speed ramps to zero, pitch falling)
/// and pauses when the platter "stops". Any other deck toggles play.</summary>
public sealed class PlaybackRequests(IDecks decks) : IPlaybackRequests
{
    private readonly IDecks _decks = decks;

    public event Action<int>? BrakePauseRequested;

    public void OnPlayPressed(int deck)
    {
        var d = _decks.DeckFor(deck);
        if (d.Loading.IsPlaying && d.Scratch.CanScratch)
        {
            BrakePauseRequested?.Invoke(deck);
            return;
        }
        d.Transport.TogglePlay();
    }
}

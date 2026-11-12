namespace Sholto.App.Decks;

/// <summary>See <see cref="IPlaybackRequests"/>. A playing, scratch-capable deck is not paused at once: the
/// request goes to the scratch engine, which runs a vinyl-style brake (speed ramps to zero, pitch falling)
/// and pauses when the platter "stops". Any other deck toggles play. During a hot-cue preview PLAY instead latches
/// the preview (see <see cref="IDeckHotCues.LatchPreview"/>): the deck keeps playing after the pad is released.</summary>
public sealed class PlaybackRequests(IDecks decks, IDeckHotCues hotCues) : IPlaybackRequests
{
    private readonly IDecks _decks = decks;
    private readonly IDeckHotCues _hotCues = hotCues;

    public event Action<int>? BrakePauseRequested;

    public void OnPlayPressed(int deck)
    {
        if (_hotCues.LatchPreview(deck)) return;
        var d = _decks.DeckFor(deck);
        if (d.Loading.IsPlaying && d.Scratch.CanScratch)
        {
            BrakePauseRequested?.Invoke(deck);
            return;
        }
        d.Transport.TogglePlay();
    }
}

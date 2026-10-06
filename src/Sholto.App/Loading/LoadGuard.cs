using Sholto.App.Decks;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Loading;

/// <summary>See <see cref="ILoadGuard"/>. Holds at most one pending confirmation; it lapses after
/// 3 s, noticed on the frame tick (allocation-free while idle).</summary>
public sealed class LoadGuard : ILoadGuard, IFrameTickHandler
{
    /// <summary>How long, in seconds, a second request may follow the first.</summary>
    private const int WindowSeconds = 3;

    /// <summary>Where the guard sits on the frame clock: the App core acts first.</summary>
    private const int FrameOrder = 50;

    private readonly IFrameClock _clock;
    private readonly IEventPublisher _publisher;
    private bool _pending;
    private int _deck;
    private string _filePath = "";
    private DateTime _until;

    public LoadGuard(IFrameClock clock, IEventPublisher publisher)
    {
        _clock = clock;
        _publisher = publisher;
        clock.Subscribe(this, FrameOrder);
    }

    public bool Permit(IDeckSession deck, Track incoming)
    {
        var now = _clock.Now;
        if (!deck.IsPlaying)
        {
            // Nothing to protect; drop a warning that was about this deck.
            if (_pending && _deck == deck.Index) Clear();
            return true;
        }
        if (_pending && now < _until && _deck == deck.Index && _filePath == incoming.FilePath)
        {
            Clear();
            return true;
        }
        _pending = true;
        _deck = deck.Index;
        _filePath = incoming.FilePath;
        _until = now.AddSeconds(WindowSeconds);
        _publisher.Publish(new LoadConfirmPending(
            true, deck.Index, incoming.Title, deck.LoadedTrack?.Title, RemainingSeconds(deck)));
        return false;
    }

    public void OnFrame(DateTime now)
    {
        if (_pending && now >= _until) Clear();
    }

    private void Clear()
    {
        var deck = _deck;
        _pending = false;
        _filePath = "";
        _publisher.Publish(new LoadConfirmPending(false, deck, null, null, 0));
    }

    /// <summary>Time left in the playing track at its current speed.</summary>
    private double RemainingSeconds(IDeckSession deck)
    {
        var track = deck.LoadedTrack;
        if (track is null) return 0;
        var speed = deck.SourceBpm > 0 && deck.EffectiveBpm > 0 ? deck.EffectiveBpm / deck.SourceBpm : 1.0;
        return Math.Max(0, (track.Duration.TotalSeconds - deck.PlaybackSeconds) / speed);
    }
}

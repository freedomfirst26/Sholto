using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App.Performance;

/// <summary>Ticks the performance buckets in a fixed order. The order is behaviour; do not reorder.</summary>
public sealed class PerformanceTick(
    IFrameClock clock, IDecks decks, IMagnetSnap magnet, IJogSeek seek, IScratchEngine scratch) : IPerformanceTick
{
    private readonly IFrameClock _clock = clock;
    private readonly IDecks _decks = decks;
    private readonly IMagnetSnap _magnet = magnet;
    private readonly IJogSeek _seek = seek;
    private readonly IScratchEngine _scratch = scratch;
    private bool _disposed;

    public void Start() => _clock.Subscribe(this, 0);

    public void OnFrame(DateTime now)
    {
        if (_disposed) return;

        // The factor is computed here for the seek damping and again inside Update; both are intended.
        double scale = 1 - _magnet.Factor() * 0.9;
        _seek.Flush(scale);

        var frameNow = _clock.Now;
        _seek.UpdateScrubbing(frameNow);

        _scratch.Tick(0, frameNow);
        _scratch.Tick(1, frameNow);

        _magnet.Update();

        if (_decks.Deck1.Loading.IsLoaded) _decks.Deck1.SyncPlayPosition();
        if (_decks.Deck2.Loading.IsLoaded) _decks.Deck2.SyncPlayPosition();
    }

    /// <summary>The clock cannot unsubscribe; stop ticking instead.</summary>
    public void Dispose() => _disposed = true;
}

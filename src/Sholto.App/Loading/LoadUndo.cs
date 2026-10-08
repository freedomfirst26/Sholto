using Microsoft.Extensions.Options;
using Sholto.App.Decks;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Loading;

/// <summary>See <see cref="ILoadUndo"/>. Lapse is noticed on the frame tick (allocation-free while idle).</summary>
public sealed class LoadUndo : ILoadUndo, IFrameTickHandler
{
    /// <summary>Where the undo sits on the frame clock: the App core acts first.</summary>
    private const int FrameOrder = 50;

    private readonly IFrameClock _clock;
    private readonly int _windowSeconds;
    private LoadRecord? _record;

    public LoadUndo(IFrameClock clock, IEventPublisher publisher, IOptions<LoadOptions> options)
    {
        _clock = clock;
        _windowSeconds = options.Value.UndoWindowSeconds;
        clock.Subscribe(this, FrameOrder);
    }

    public LoadRecord Capture(IDeckSession deck, Track incoming) =>
        new(deck.Index, deck.LoadedTrack, deck.PlayPosition, incoming, _clock.Now);

    public void Commit(LoadRecord record) => _record = record;

    public LoadRecord? Take()
    {
        if (_record is not { } record) return null;
        var live = _clock.Now < record.At.AddSeconds(_windowSeconds);
        Clear();
        return live ? record : null;
    }

    public void OnFrame(DateTime now)
    {
        if (_record is { } record && now >= record.At.AddSeconds(_windowSeconds)) Clear();
    }

    private void Clear() => _record = null;
}

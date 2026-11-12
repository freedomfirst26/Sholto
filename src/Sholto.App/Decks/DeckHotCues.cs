using Microsoft.Extensions.Options;
using Sholto.App.Library;
using Sholto.App.Library.Markers;
using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>See <see cref="IDeckHotCues"/>.</summary>
public sealed class DeckHotCues(
    IDecks decks, ILibrarySession library, IAppThread appThread, IEventPublisher publisher,
    IDeckMarkers markers, IOptions<HotCueOptions> options) : IDeckHotCues
{
    private const int PadCount = 8;

    private readonly IDecks _decks = decks;
    private readonly ILibrarySession _library = library;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;
    private readonly IDeckMarkers _markers = markers;
    private readonly HotCueOptions _options = options.Value;
    private readonly HotCue?[][] _cues = [new HotCue?[PadCount], new HotCue?[PadCount]];
    private readonly bool[] _cuesLoaded = new bool[2];
    private readonly int[] _previewPad = [-1, -1];
    private readonly double[] _previewSec = new double[2];
    private IHotCueStore? _store;
    private Task _writes = Task.CompletedTask;

    public void Attach(IHotCueStore store)
    {
        _store = store;
        _decks.Deck1.LoadStateChanged += s => OnLoadState(0, s);
        _decks.Deck2.LoadStateChanged += s => OnLoadState(1, s);
        // A deck already loaded has no Loaded event coming: read its cues now, or its pads stay locked.
        if (_decks.Deck1.LoadState == DeckLoadState.Loaded) OnLoadState(0, DeckLoadState.Loaded);
        if (_decks.Deck2.LoadState == DeckLoadState.Loaded) OnLoadState(1, DeckLoadState.Loaded);
    }

    public void Press(int deck, int pad, bool pressed)
    {
        if (_store is null || (uint)pad >= PadCount) return;
        var session = _decks.DeckFor(deck);
        if (!pressed)
        {
            Release(deck, pad, session);
            return;
        }
        if (!session.IsLoaded || !_cuesLoaded[deck]) return;
        if (_cues[deck][pad] is { } cue) Jump(deck, pad, cue, session);
        else Set(deck, pad, session);
    }

    public bool LatchPreview(int deck)
    {
        if (_previewPad[deck] < 0) return false;
        _previewPad[deck] = -1;   // the release now finds no preview: the deck plays on
        return true;
    }

    public void Delete(int deck, int pad)
    {
        var store = _store;
        if (store is null || (uint)pad >= PadCount || _cues[deck][pad] is null) return;
        var session = _decks.DeckFor(deck);
        var trackId = TrackIdOf(session);
        _cues[deck][pad] = null;
        Publish(deck);
        if (trackId != Guid.Empty) Persist(deck, "delete", () => store.DeleteAsync(trackId, pad));
    }

    private void Set(int deck, int pad, IDeckSession session)
    {
        var store = _store!;
        var trackId = TrackIdOf(session);
        if (trackId == Guid.Empty) return;
        var cue = new HotCue(pad, PositionToSave(session), _options.DefaultColour);
        _cues[deck][pad] = cue;
        Publish(deck);
        Persist(deck, "save", () => store.SetAsync(trackId, cue));
    }

    private double PositionToSave(IDeckSession session)
    {
        double playhead = session.PlaybackSeconds;
        var grid = session.SectionGrid;
        if (!_options.SnapToBeat || grid.IsEmpty) return playhead;
        return Math.Max(0, grid.BeatAt(grid.NearestBeatIndex(playhead)));
    }

    private void Jump(int deck, int pad, HotCue cue, IDeckSession session)
    {
        session.Transport.SeekRelative(cue.PositionSec - session.PlaybackSeconds);
        if (!session.IsPlaying)
        {
            session.TogglePlay();
            if (!session.IsPlaying) return;   // refused: not playable yet
        }
        else if (_previewPad[deck] < 0) return;   // was playing: keep playing
        _previewPad[deck] = pad;
        _previewSec[deck] = cue.PositionSec;
    }

    private void Release(int deck, int pad, IDeckSession session)
    {
        if (_previewPad[deck] != pad) return;
        _previewPad[deck] = -1;
        if (session.IsPlaying) session.TogglePlay();
        session.Transport.SeekRelative(_previewSec[deck] - session.PlaybackSeconds);
    }

    private void OnLoadState(int deck, DeckLoadState state)
    {
        if (state is DeckLoadState.Loading or DeckLoadState.Idle or DeckLoadState.Failed)
        {
            Clear(deck);
            return;
        }
        if (state != DeckLoadState.Loaded) return;
        _cuesLoaded[deck] = false;
        _ = LoadForDeckAsync(deck);
    }

    private void Clear(int deck)
    {
        _cuesLoaded[deck] = false;
        Array.Clear(_cues[deck]);
        _previewPad[deck] = -1;
        Publish(deck);
    }

    private async Task LoadForDeckAsync(int deck)
    {
        try
        {
            var store = _store;
            if (store is null) return;
            var session = _decks.DeckFor(deck);
            var trackId = TrackIdOf(session);
            if (trackId == Guid.Empty) return;
            await _writes;   // a save made just before this load must be listed
            var saved = await store.ListAsync(trackId);
            await _appThread.InvokeAsync(() =>
            {
                // Another track may have started loading while the store answered.
                if (TrackIdOf(session) != trackId) return;
                Array.Clear(_cues[deck]);
                foreach (var cue in saved)
                    if ((uint)cue.Pad < PadCount) _cues[deck][cue.Pad] = cue;
                _cuesLoaded[deck] = true;
                Publish(deck);
            });
        }
        catch (Exception ex)
        {
            // The pads stay locked: a press must not overwrite cues that could not be read.
            Console.WriteLine($"[HotCue] load failed: {ex.Message}");
        }
    }

    /// <summary>Queues a store write behind every earlier one, so writes reach the store in the order they were made.</summary>
    private void Persist(int deck, string op, Func<Task> write) => _writes = ChainAsync(_writes, deck, op, write);

    private async Task ChainAsync(Task previous, int deck, string op, Func<Task> write)
    {
        await previous;   // never faults: every link catches
        try
        {
            await write();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HotCue] {op} failed: {ex.Message}");
            return;
        }
        try
        {
            await _markers.RefreshAsync(deck);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HotCue] refresh failed: {ex.Message}");
        }
    }

    private Guid TrackIdOf(IDeckSession session) =>
        session.LoadedTrack is { } track ? _library.TrackIdFor(track.FilePath) : Guid.Empty;

    private void Publish(int deck)
    {
        var list = new List<HotCue>(PadCount);
        foreach (var cue in _cues[deck])
            if (cue is { } c) list.Add(c);
        _publisher.Publish(new DeckHotCuesChanged(deck, list.ToArray()));
    }
}

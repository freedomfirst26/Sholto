using Sholto.App.Library.Markers;
using Sholto.Data;
using Sholto.App.Library;

namespace Sholto.App.Decks;

/// <summary>See <see cref="IDeckMarkers"/>.</summary>
public sealed class DeckMarkers(IDecks decks, ILibrarySession library, IAppThread appThread, IEventPublisher publisher) : IDeckMarkers
{
    private readonly IDecks _decks = decks;
    private readonly ILibrarySession _library = library;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;
    private IMarkerService? _markers;

    public event Action<int, double>? MarkerAdded;

    public void Attach(IMarkerService markers)
    {
        _markers = markers;
        // Load a track's saved markers onto its deck whenever a load completes, across every load path
        // (keyboard, controller, Enter-mode).
        _decks.Deck1.LoadStateChanged += s => { if (s == DeckLoadState.Loaded) _ = LoadForDeckAsync(_decks.Deck1); };
        _decks.Deck2.LoadStateChanged += s => { if (s == DeckLoadState.Loaded) _ = LoadForDeckAsync(_decks.Deck2); };
    }

    public async Task AddAsync(int deck)
    {
        var markers = _markers;
        if (markers is null) return;
        var session = _decks.DeckFor(deck);
        if (!session.Loading.IsLoaded) return;
        var trackId = _library.TrackIdFor(session.LoadedTrack?.FilePath ?? "");
        if (trackId == Guid.Empty) return;
        double secs = session.PlaybackSeconds;
        await markers.AddAsync(trackId, secs);
        await LoadForDeckAsync(session);
        MarkerAdded?.Invoke(deck, secs);
        // A toast-worthy fact for the interfaces; this continuation may be off the app thread.
        _appThread.Post(() => _publisher.Publish(new Sholto.Data.MarkerAdded(deck, secs)));
    }

    private async Task LoadForDeckAsync(IDeckSession session)
    {
        var markers = _markers;
        if (markers is null) return;
        var path = session.LoadedTrack?.FilePath;
        if (path is null) return;
        var trackId = _library.TrackIdFor(path);
        if (trackId == Guid.Empty) return;
        var secs = (await markers.ListPositionsAsync(trackId)).ToArray();
        await _appThread.InvokeAsync(() => session.SetMarkers(secs));
    }
}

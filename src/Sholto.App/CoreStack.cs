using Sholto.App.Decks;
using Sholto.App.Library;
using Sholto.App.Loading;
using Sholto.App.Mixer;

namespace Sholto.App;

/// <summary>The headless application core, built together over the two deck sessions: the decks, the
/// play/brake requests, the mixer, the library, deck markers and the track loader. No Avalonia types,
/// no dispatcher: interfaces and the composition root take what they need from it.</summary>
public sealed class CoreStack(
    IDecks decks,
    IPlaybackRequests playback,
    IMixer mixer,
    ILibrarySession library,
    IDeckMarkers markers,
    ITrackLoader loader)
{
    public IDecks Decks { get; } = decks;
    public IPlaybackRequests Playback { get; } = playback;
    public IMixer Mixer { get; } = mixer;
    public ILibrarySession Library { get; } = library;
    public IDeckMarkers Markers { get; } = markers;
    public ITrackLoader Loader { get; } = loader;
}

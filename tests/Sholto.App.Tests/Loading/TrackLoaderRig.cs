using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;
using Sholto.App.Loading;

namespace Sholto.App.Tests;

/// <summary>The production <see cref="TrackLoader"/> over a scanned <see cref="LibrarySessionRig"/> (the three
/// tracks, none selected yet), a fake decoder and key analyzer, and a deck 1 whose re-analysis provider is
/// <paramref name="analysisProvider"/>. The app thread is one dedicated thread, as in the app.</summary>
internal sealed class TrackLoaderRig
{
    public static readonly Origin Origin = new(InterfaceIds.Bench, "test", "load");

    public TrackLoaderRig(
        FakeAudioFileDecoder decoder, IAnalysisProvider? analysisProvider = null, Key? foundKey = null)
    {
        var appThread = new SingleThreadAppThread();
        Library = new LibrarySessionRig(appThread, analysisProvider);
        Decoder = decoder;
        KeyStore = new RecordingKeyAnalysisStore();
        Loader = new TrackLoader(
            Library.Library, new DeckPair(Library.Deck1, Library.Deck2), decoder, KeyStore,
            new FakeKeyAnalyzer(foundKey), Library.Reporter, appThread, Library.Bus);
        // On the pool, so the scan does not capture the test framework's synchronization context and wait on this blocked thread.
        Task.Run(() => Library.Library.ScanAsync("/music", null)).GetAwaiter().GetResult();
    }

    public LibrarySessionRig Library { get; }
    public FakeAudioFileDecoder Decoder { get; }
    public RecordingKeyAnalysisStore KeyStore { get; }
    public TrackLoader Loader { get; }

    /// <summary>Highlight the row for <paramref name="track"/>.</summary>
    public void Highlight(Track track)
    {
        var index = Library.Library.Rows.ToList().FindIndex(r => r.FilePath == track.FilePath);
        Library.Library.Select(index);
    }
}

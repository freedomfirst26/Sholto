using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stores;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App.Loading;

/// <summary>See <see cref="ITrackLoader"/>. Decoding runs off the app thread; the decoded samples (or the
/// failure) are handed back to the deck on it.</summary>
public sealed class TrackLoader(
    ILibrarySession library,
    IDecks decks,
    IAudioFileDecoder decoder,
    IKeyAnalysisStore keyStore,
    IKeyAnalyzer keyAnalyzer,
    IAnalysisReporter reporter,
    IAppThread appThread,
    IEventPublisher publisher) : ITrackLoader
{
    private readonly ILibrarySession _library = library;
    private readonly IDecks _decks = decks;
    private readonly IAudioFileDecoder _decoder = decoder;
    // The composition root passes the switchable store, which is a no-op until the DB attaches.
    private readonly IKeyAnalysisStore _keyStore = keyStore;
    private readonly IKeyAnalyzer _keyAnalyzer = keyAnalyzer;
    private readonly IAnalysisReporter _reporter = reporter;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;

    /// <summary>Load the highlighted library track into a deck: show it at once, decode off the app thread,
    /// then hand the samples to the deck. A failed decode leaves the deck usable.</summary>
    public void Handle(in LoadSelectedIntoDeck command)
    {
        var track = _library.SelectedTrack;
        if (track is null) return;
        var deckIndex = command.Deck;
        var deck = _decks.DeckFor(deckIndex);
        var mult = _library.GetBpmMultiplierFor(track.FilePath);
        deck.BeginLoad(track, mult);
        _ = DecodeAndLoadAsync(deck, deckIndex, track, mult);
    }

    private async Task DecodeAndLoadAsync(IDeckSession deck, int deckIndex, Track track, double mult)
    {
        float[] samples;
        try
        {
            samples = await Task.Run(() => _decoder.Decode(track.FilePath)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Track] load into deck {deckIndex + 1} FAILED: {ex.Message}");
            _appThread.Post(() => FailLoad(deck, deckIndex, track));
            return;
        }
        _appThread.Post(() =>
        {
            try
            {
                deck.LoadTrack(track, track.FilePath, samples, mult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Track] load into deck {deckIndex + 1} FAILED: {ex.Message}");
                FailLoad(deck, deckIndex, track);
            }
        });
    }

    /// <summary>The deck stays usable; say so, so an interface can tell the user. App thread.</summary>
    private void FailLoad(IDeckSession deck, int deckIndex, Track track)
    {
        deck.LoadFailed();
        _publisher.Publish(new TrackLoadFailed(deckIndex, track.FilePath, track.Title));
    }

    /// <summary>Re-analyse the highlighted library track (browse knob held, or a library double-click).</summary>
    public void Handle(in ReanalyzeSelected command)
    {
        var track = _library.SelectedTrack;
        if (track is null) return;
        var provider = _decks.Deck1.Loading.AnalysisProvider;
        if (provider is null)
        {
            Console.WriteLine($"[TrackLoader] {command.Origin.GestureName} re-analyze: no AnalysisProvider yet");
            return;
        }
        Console.WriteLine($"[TrackLoader] {command.Origin.GestureName} → re-analyzing {track.FilePath}");
        _ = ReanalyzeAsync(track, provider);
    }

    /// <summary>Force-reanalyze a library track: recompute BPM/beats/peaks (BasicAnalysis) AND the Camelot
    /// key, overwriting the matching cache tiers, then update the library row in place and re-broadcast the
    /// harmony reference so the dimming refreshes.</summary>
    private async Task ReanalyzeAsync(Track track, Sholto.App.Analysis.Analyzers.IAnalysisProvider provider)
    {
        try
        {
            var samples = await Task.Run(() => _decoder.Decode(track.FilePath)).ConfigureAwait(false);
            int rate = AudioFileDecoder.TargetSampleRate;

            var decodedTrack = new DecodedTrack(track.FilePath, samples, rate, AudioFileDecoder.TargetChannels);
            var basicTask = provider.RecomputeAsync(decodedTrack);
            var keyTask = _keyAnalyzer.AnalyzeAsync(decodedTrack, reporter: _reporter);

            var analysis = await basicTask.ConfigureAwait(false);
            var key = await keyTask.ConfigureAwait(false);
            try { await _keyStore.PutAsync(track.FilePath, key).ConfigureAwait(false); }
            catch (Exception ex) { Console.WriteLine($"[TrackLoader] re-analyze key cache write failed: {ex.Message}"); }

            _appThread.Post(() => _library.ApplyReanalysis(track.FilePath, analysis.Bpm, key.Key));
            Console.WriteLine($"[TrackLoader] re-analyzed {track.FilePath}: {analysis.Bpm:F1} BPM, key {key.Key?.ToCamelot()}");
        }
        catch (Exception ex)
        {
            // Surface it on the row as well as in the log — a re-analysis that throws here never touches
            // the reporter, so without this the row would silently keep whatever it had and the user
            // would never learn it didn't run.
            Console.WriteLine($"[TrackLoader] re-analyze failed: {ex.Message}");
            var failure = $"{ex.GetType().Name}: {ex.Message}";
            _appThread.Post(() => _library.ReportReanalysisFailure(track.FilePath, failure));
        }
    }
}

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
    IEventPublisher publisher,
    ISearchPick pick,
    ILoadGuard guard,
    ILoadUndo undo) : ITrackLoader
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
    private readonly ISearchPick _pick = pick;
    private readonly ILoadGuard _guard = guard;
    private readonly ILoadUndo _undo = undo;
    // Per deck, the id of its latest load. A decode that finishes after a newer load began is dropped. App thread.
    private readonly Dictionary<int, int> _generations = [];

    public event Action<int, Track>? Accepted;

    /// <summary>The track LOAD and re-analyse mean: the search pick while search is active (null picks nothing),
    /// otherwise the highlighted library row.</summary>
    private Track? ResolveTrack() => _pick.Active ? _pick.PickedTrack : _library.SelectedTrack;

    /// <summary>Load the highlighted library track into a deck: show it at once, decode off the app thread,
    /// then hand the samples to the deck. A failed decode leaves the deck usable.</summary>
    public void Handle(in LoadSelectedIntoDeck command)
    {
        var track = ResolveTrack();
        if (track is null) return;
        var deckIndex = command.Deck;
        var deck = _decks.DeckFor(deckIndex);
        if (!_guard.Permit(deck, track)) return;
        var record = _undo.Capture(deck, track);
        StartLoad(deck, deckIndex, track, null);
        _undo.Commit(record);
        _publisher.Publish(new LoadAccepted(deckIndex, track.FilePath, track.Title, track.Artist));
        Accepted?.Invoke(deckIndex, track);
    }

    /// <summary>Undo the last accepted load, if it is still within its window: bump the deck's generation so an
    /// in-flight decode of the loaded track never lands, then empty the deck or load the previous track back
    /// (no guard, no <see cref="LoadAccepted"/>, no new undo record) at its previous position. The deck is left
    /// paused.</summary>
    public void Handle(in UndoLastLoad command)
    {
        if (_undo.Take() is not { } record) return;
        var deck = _decks.DeckFor(record.Deck);
        if (record.Previous is null)
        {
            _generations[record.Deck] = _generations.GetValueOrDefault(record.Deck) + 1;
            deck.Unload();
        }
        else
        {
            StartLoad(deck, record.Deck, record.Previous, record.PreviousPosition);
        }
    }

    /// <summary>Show the track on the deck at once and decode it off the app thread, superseding any earlier load
    /// of the deck. When <paramref name="seekTo"/> is set the playhead goes there once the track lands.</summary>
    private int StartLoad(IDeckSession deck, int deckIndex, Track track, double? seekTo)
    {
        var mult = _library.GetBpmMultiplierFor(track.FilePath);
        var generation = _generations.GetValueOrDefault(deckIndex) + 1;
        _generations[deckIndex] = generation;
        deck.BeginLoad(track, mult);
        _ = DecodeAndLoadAsync(deck, deckIndex, track, mult, generation, seekTo);
        return generation;
    }

    private async Task DecodeAndLoadAsync(
        IDeckSession deck, int deckIndex, Track track, double mult, int generation, double? seekTo)
    {
        float[] samples;
        try
        {
            samples = await Task.Run(() => _decoder.Decode(track.FilePath)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Track] load into deck {deckIndex + 1} FAILED: {ex.Message}");
            _appThread.Post(() =>
            {
                if (IsSuperseded(deckIndex, generation)) return;
                FailLoad(deck, deckIndex, track);
            });
            return;
        }
        _appThread.Post(() =>
        {
            if (IsSuperseded(deckIndex, generation)) return;
            try
            {
                deck.LoadTrack(track, track.FilePath, samples, mult);
                if (seekTo is { } fraction) deck.Transport.SeekToFraction(fraction);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Track] load into deck {deckIndex + 1} FAILED: {ex.Message}");
                FailLoad(deck, deckIndex, track);
            }
        });
    }

    /// <summary>A newer load has begun on this deck, so this one's result must not touch it. App thread.</summary>
    private bool IsSuperseded(int deckIndex, int generation) => _generations[deckIndex] != generation;

    /// <summary>The deck stays usable; say so, so an interface can tell the user. App thread.</summary>
    private void FailLoad(IDeckSession deck, int deckIndex, Track track)
    {
        deck.LoadFailed();
        _publisher.Publish(new TrackLoadFailed(deckIndex, track.FilePath, track.Title));
    }

    /// <summary>Re-analyse the highlighted library track, or the search pick while search is active (browse knob held, or a library double-click).</summary>
    public void Handle(in ReanalyzeSelected command)
    {
        var track = ResolveTrack();
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

using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.App.Decks;
using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>See <see cref="ILibraryDeckWiring"/>. Subscribes at construction, in this order: the session's
/// <c>TrackPlayed</c>, deck 1, deck 2, the analysis reporter.</summary>
public sealed class LibraryDeckWiring : ILibraryDeckWiring
{
    private readonly ILibraryCatalog _catalog;
    private readonly ISession _session;
    private readonly IAppThread _appThread;
    private readonly ILog _log;
    private ITempoMultiplierStore? _multiplierStore;

    public LibraryDeckWiring(
        ILibraryCatalog catalog,
        IDecks decks,
        ISession session,
        IAnalysisReporter reporter,
        IAppThread appThread,
        ILog log)
    {
        _catalog = catalog;
        _session = session;
        _appThread = appThread;
        _log = log;

        // Route Session events into the matching summary so the library re-renders the italic style.
        _session.TrackPlayed += filePath => _appThread.Post(() => ApplyPlayed(filePath));

        WireDeck(decks.Deck1);
        WireDeck(decks.Deck2);

        // Surface analysis progress AND failure on the row. Reading only IsBusy here was the bug that hid
        // five days of broken demucs runs: a step flipping to Failed makes IsBusy go false exactly like
        // success, so the row just quietly stopped spinning and showed nothing at all.
        //
        // Gate the failure computation on HasFailure: Updated fires on every single PropertyChanged from
        // any step (including plain progress ticks), and FailureMessage does a lock + LINQ + string.Join
        // over every step. Without this gate, every demucs tqdm tick on the analyser thread pays for that
        // work. It runs on the reporter's thread; only the summary update is posted to the app thread.
        reporter.Updated += report =>
        {
            var busy = report.IsBusy;
            var hasFailure = report.HasFailure;
            var failure = hasFailure ? report.FailureMessage : null;
            var requiredFailure = hasFailure && report.HasRequiredFailure;
            var filePath = report.FilePath;
            _appThread.Post(() => ApplyAnalysisReport(filePath, busy, failure, requiredFailure));
        };
    }

    public event Action? DeckAnalysisApplied;

    public void AttachMultiplierStore(ITempoMultiplierStore store) => _multiplierStore = store;

    private void WireDeck(IDeckSession deck)
    {
        // Played tracking: a track that finishes loading into a deck is marked played. Routed through the
        // deck's typed LoadStateChanged event rather than polling or hooking into LoadTrack itself, which
        // keeps the deck logic ignorant of session state.
        deck.LoadStateChanged += state =>
        {
            if (state != DeckLoadState.Loaded) return;
            var path = deck.LoadedTrack?.FilePath;
            if (!string.IsNullOrEmpty(path)) _session.MarkPlayed(path!);
        };

        // The user halved/doubled a loaded track's BPM: show it on the row and persist it.
        deck.BpmMultiplierChosen += OnBpmMultiplierChosen;

        // Analysis landing on a loaded deck updates its row.
        deck.Loading.AnalysisUpdated += () =>
        {
            var path = deck.LoadedTrack?.FilePath;
            if (path is null) return;
            var bpm = deck.Analysis.Basic?.Bpm;
            var stems = deck.Analysis.Get<StemPaths>();
            var key = deck.Analysis.Get<KeyAnalysis>()?.Key;
            _appThread.Post(() => ApplyDeckAnalysis(path, bpm, stems is not null, key));
        };
    }

    private void ApplyPlayed(string filePath)
    {
        if (_catalog.TryFind(filePath, out var i, out var s) && !s.IsPlayed) _catalog.Replace(i, s with { IsPlayed = true });
    }

    private void ApplyDeckAnalysis(string filePath, double? bpm, bool hasStems, Key? key)
    {
        if (_catalog.TryFind(filePath, out var i, out var s))
        {
            var updated = s;
            if (bpm is not null && updated.Bpm != bpm) updated = updated with { Bpm = bpm };
            if (hasStems && !updated.StemsReady) updated = updated with { StemsReady = true };
            if (key is { } k && updated.MusicalKey != k.ToRef()) updated = updated with { MusicalKey = k.ToRef() };
            if (!ReferenceEquals(updated, s)) _catalog.Replace(i, updated);
        }
        DeckAnalysisApplied?.Invoke();
    }

    private void ApplyAnalysisReport(string filePath, bool busy, string? failure, bool requiredFailure)
    {
        if (!_catalog.TryFind(filePath, out var i, out var s)) return;
        if (s.IsAnalyzing == busy && s.AnalysisFailure == failure && s.HasRequiredFailure == requiredFailure) return;
        _catalog.Replace(i, s with { IsAnalyzing = busy, AnalysisFailure = failure, HasRequiredFailure = requiredFailure });
    }

    private void OnBpmMultiplierChosen(string filePath, double multiplier)
    {
        // Also update the matching row so the library list reflects the change.
        if (_catalog.TryFind(filePath, out var i, out var s) && s.BpmMultiplier != multiplier)
            _catalog.Replace(i, s with { BpmMultiplier = multiplier });
        var store = _multiplierStore;
        if (store is not null) _ = PersistMultiplierAsync(store, filePath, multiplier);
    }

    private async Task PersistMultiplierAsync(ITempoMultiplierStore store, string filePath, double multiplier)
    {
        try
        {
            await store.PutAsync(filePath, multiplier);
        }
        catch (Exception ex) { _log.Error($"save bpm override failed: {ex.Message}"); }
    }
}

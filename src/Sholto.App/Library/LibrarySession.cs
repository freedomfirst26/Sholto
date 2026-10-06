using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.App.Library;
using Sholto.App.Library.Crates;
using Sholto.App.Library.Tags;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App.Library;

/// <summary>See <see cref="ILibrarySession"/>. The catalog (<c>_all</c>) is the source of truth; the
/// visible rows (<c>_visible</c>) are the catalog or a filtered subset of it, in the same order. Every
/// mutation happens on the app thread: events from other threads (the analysis reporter, a deck's analysis
/// landing, the tag service) are posted onto it first.</summary>
public sealed class LibrarySession : ILibrarySession
{
    private readonly ITrackScanner _trackScanner;
    private readonly IKeyAnalysisStore _keyStore;
    private readonly IStemPresence _stemPresence;
    private readonly IDecks _decks;
    private readonly ISession _session;
    private readonly IAppThread _appThread;
    private readonly IEventPublisher _publisher;

    private List<TrackSummary> _all = [];
    private List<TrackSummary> _visible = [];
    private readonly Dictionary<string, int> _allIndex = [];
    private readonly Dictionary<string, int> _visibleIndex = [];
    private int _version;
    private int _selectedIndex = -1;

    private ICrateService? _crates;
    private ITagService? _tags;
    private ITempoMultiplierStore? _multiplierStore;

    public LibrarySession(
        ITrackScanner trackScanner,
        IKeyAnalysisStore keyStore,
        IStemPresence stemPresence,
        IAnalysisReporter reporter,
        IDecks decks,
        ISession session,
        IAppThread appThread,
        IEventPublisher publisher)
    {
        _trackScanner = trackScanner;
        _keyStore = keyStore;
        _stemPresence = stemPresence;
        _decks = decks;
        _session = session;
        _appThread = appThread;
        _publisher = publisher;

        // Route Session events into the matching summary so the library re-renders the italic style.
        _session.TrackPlayed += filePath => _appThread.Post(() => ApplyPlayed(filePath));

        WireDeck(_decks.Deck1);
        WireDeck(_decks.Deck2);

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

    public event Action<IReadOnlyList<TrackSummary>>? RowsChanged;
    public event Action<TrackSummary>? RowUpdated;
    public event Action? UnreachableChanged;
    public event Action<Key?>? HarmonyReferenceChanged;
    public event Action<ITagService, ICrateService>? ServicesAttached;
    public event Action? SelectedIndexChanged;

    public IReadOnlyList<TrackSummary> Rows => _visible;

    public IReadOnlyList<TrackSummary> Catalog => _all;

    public ITagService? Tags => _tags;

    public ICrateService? Crates => _crates;

    public string? CurrentDir { get; private set; }

    public bool IsScanning { get; private set; }

    public string? UnreachablePath { get; private set; }

    public bool IsUnreachable => !string.IsNullOrEmpty(UnreachablePath);

    public string? ActiveFilter { get; private set; }

    public Key? HarmonyReferenceKey { get; private set; }

    // ---- Scan ----------------------------------------------------------------------------------

    public async Task ScanAsync(string musicDir, LibraryStack? stores)
    {
        if (string.IsNullOrEmpty(musicDir)) return;
        await _appThread.InvokeAsync(() => ClearFilter());
        IsScanning = true;
        try
        {
            Console.WriteLine($"[Library] scanning {musicDir}");
            var scanned = await _trackScanner.ScanAsync(musicDir);

            Dictionary<string, double>? cachedBpms = null;
            Dictionary<string, double>? cachedMults = null;
            Dictionary<string, Key>? cachedKeys = null;
            Dictionary<string, Guid>? cachedIds = null;
            Dictionary<string, IReadOnlyList<string>>? cachedTagsByPath = null;

            if (stores is not null)
            {
                var upsert = await stores.Tracks.UpsertAsync(scanned);
                var pathToId = upsert.PathToId;
                cachedIds = pathToId.ToDictionary(kv => kv.Key, kv => kv.Value);

                // Songs that just appeared in the folder get filed into the "All Tracks" crate (once the
                // crate service is attached).
                var crates = _crates;
                if (crates is not null) _ = FileIntoAllTracksAsync(crates, upsert.NewIds);

                var tagsGrouped = await stores.Tags.GetTagsByTrackAsync();

                cachedTagsByPath = pathToId.ToDictionary(
                    kv => kv.Key,
                    kv => tagsGrouped.TryGetValue(kv.Value, out var tags)
                        ? tags
                        : (IReadOnlyList<string>)Array.Empty<string>());

                cachedBpms = new Dictionary<string, double>(await stores.BasicAnalyses.GetDetectedBpmsAsync());
                cachedMults = new Dictionary<string, double>(await stores.TempoMultipliers.GetAllAsync());

                cachedKeys = new();
                foreach (var (path, key) in await _keyStore.GetAllAsync())
                    if (key.Key is { } k) cachedKeys[path] = k;
            }

            var summaries = new List<TrackSummary>(scanned.Count);
            int missing = 0;
            foreach (var t in scanned.OrderBy(t => t.Artist).ThenBy(t => t.Title))
            {
                var summary = t.ToSummary();
                if (cachedBpms is not null && cachedBpms.TryGetValue(t.FilePath, out var bpm))
                    summary = summary with { Bpm = bpm };
                if (cachedMults is not null && cachedMults.TryGetValue(t.FilePath, out var m))
                    summary = summary with { BpmMultiplier = m };
                if (cachedKeys is not null && cachedKeys.TryGetValue(t.FilePath, out var k))
                    summary = summary with { MusicalKey = k.ToRef() };
                if (cachedIds is not null)
                {
                    if (cachedIds.TryGetValue(t.FilePath, out var id)) summary = summary with { TrackId = id };
                    else { missing++; if (missing <= 3) Console.WriteLine($"[Library] no TrackId for: {t.FilePath}"); }
                }
                if (cachedTagsByPath is not null && cachedTagsByPath.TryGetValue(t.FilePath, out var tags))
                    summary = summary with { Tags = tags };
                summaries.Add(summary);
            }
            if (missing > 0)
                Console.WriteLine($"[Library] {missing} row(s) missing TrackId after scan (paths not in the catalog)");

            await _appThread.InvokeAsync(() =>
            {
                ReplaceCatalog(summaries);
                CurrentDir = musicDir;
                SetUnreachablePath(null);
            });

            // After every scan: file every track into "All Tracks" (backfills any not yet a member),
            // refresh the harmony reference and walk the new rows to see which already have stems on disk.
            var crateService = _crates;
            if (crateService is not null)
                _ = FileIntoAllTracksAsync(crateService, summaries.Select(s => s.TrackId).ToList());
            _appThread.Post(() =>
            {
                RefreshHarmonyReference();
                _ = HydrateStemStateAsync();
            });
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void SetUnreachablePath(string? path)
    {
        if (UnreachablePath == path) return;
        UnreachablePath = path;
        UnreachableChanged?.Invoke();
        _publisher.Publish(new LibraryUnreachableChanged(path));
    }

    /// <summary>"All Tracks" is a real crate every song belongs to; get-or-create it and add the tracks.</summary>
    private async Task FileIntoAllTracksAsync(ICrateService crates, IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0) return;
        int crateId = await crates.CreateAsync(CrateNames.AllTracks);
        foreach (var id in ids) await crates.AddTrackAsync(crateId, id);
    }

    // ---- Catalog and visible rows --------------------------------------------------------------

    private void ReplaceCatalog(List<TrackSummary> summaries)
    {
        _all = summaries;
        _allIndex.Clear();
        for (var i = 0; i < _all.Count; i++) _allIndex[_all[i].FilePath] = i;
        SetActiveFilter(null);
        SetVisible([.. _all]);
    }

    private void SetActiveFilter(string? label)
    {
        ActiveFilter = label;
        _publisher.Publish(new LibraryFilterChanged(label));
    }

    private void SetVisible(List<TrackSummary> rows)
    {
        _visible = rows;
        _visibleIndex.Clear();
        for (var i = 0; i < _visible.Count; i++) _visibleIndex[_visible[i].FilePath] = i;
        _version++;
        // The old rows are gone, so the old highlight is too (the list box used to report -1 as its items
        // were cleared).
        SetSelectedIndex(-1);
        // One immutable snapshot, shared by the C# event and the bus.
        var snapshot = _visible.ToArray();
        RowsChanged?.Invoke(snapshot);
        _publisher.Publish(new LibraryRowsChanged(snapshot, _version));
    }

    /// <summary>Store a changed summary in the catalog and the visible rows and announce it.</summary>
    private void Replace(int allIndex, TrackSummary updated)
    {
        _all[allIndex] = updated;
        if (_visibleIndex.TryGetValue(updated.FilePath, out var visibleIndex)) _visible[visibleIndex] = updated;
        RowUpdated?.Invoke(updated);
        _publisher.Publish(new TrackSummaryChanged(updated));
    }

    private bool TryFind(string filePath, out int allIndex, out TrackSummary summary)
    {
        if (_allIndex.TryGetValue(filePath, out allIndex))
        {
            summary = _all[allIndex];
            return true;
        }
        summary = null!;
        return false;
    }

    public TrackSummary? SummaryFor(string filePath) =>
        TryFind(filePath, out _, out var summary) ? summary : null;

    public double GetBpmMultiplierFor(string filePath) =>
        TryFind(filePath, out _, out var summary) ? summary.BpmMultiplier : 1.0;

    public Guid TrackIdFor(string filePath) =>
        TryFind(filePath, out _, out var summary) ? summary.TrackId : Guid.Empty;

    // ---- Filters -------------------------------------------------------------------------------

    public async Task FilterByTagAsync(string tag)
    {
        var tags = _tags;
        if (tags is null) return;
        var ids = await tags.GetTrackIdsForTagAsync(tag, default);
        await _appThread.InvokeAsync(() => ApplyFilter(tag, ids));
    }

    public async Task FilterByCrateAsync(CrateSummary crate)
    {
        var crates = _crates;
        if (crates is null) return;
        var ids = await crates.TrackIdsAsync(crate.Id);
        // The label carries a package emoji so the active-filter chip reads as a crate, not a tag.
        await _appThread.InvokeAsync(() => ApplyFilter($"📦 {crate.Name}", ids));
    }

    /// <summary>Filter from the whole catalog (a second filter replaces the first, it does not nest).</summary>
    private void ApplyFilter(string label, IEnumerable<Guid> trackIds)
    {
        var allowed = new HashSet<Guid>(trackIds);
        var rows = new List<TrackSummary>();
        foreach (var summary in _all)
            if (allowed.Contains(summary.TrackId)) rows.Add(summary);
        SetActiveFilter(label);
        SetVisible(rows);
    }

    public void ClearFilter()
    {
        if (ActiveFilter is null) return;
        SetActiveFilter(null);
        SetVisible([.. _all]);
    }

    // ---- Selection -----------------------------------------------------------------------------

    public int SelectedIndex => _selectedIndex;

    public TrackSummary? SelectedSummary =>
        _selectedIndex >= 0 && _selectedIndex < _visible.Count ? _visible[_selectedIndex] : null;

    public Track? SelectedTrack => SelectedSummary?.ToTrack();

    public void SetSelectedIndex(int index)
    {
        if (_selectedIndex == index) return;
        _selectedIndex = index;
        SelectedIndexChanged?.Invoke();
        _publisher.Publish(new SelectionChanged(_selectedIndex, SelectedSummary?.TrackId ?? Guid.Empty));
    }

    public void Select(int index)
    {
        if (_visible.Count == 0) return;
        SetSelectedIndex(Math.Clamp(index, 0, _visible.Count - 1));
    }

    public void Rotate(int delta)
    {
        if (_visible.Count == 0) return;
        int next = _selectedIndex < 0 ? 0 : _selectedIndex + delta;
        Select(next);
    }

    // ---- Per-track facts -----------------------------------------------------------------------

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
        if (TryFind(filePath, out var i, out var s) && !s.IsPlayed) Replace(i, s with { IsPlayed = true });
    }

    private void ApplyDeckAnalysis(string filePath, double? bpm, bool hasStems, Key? key)
    {
        if (TryFind(filePath, out var i, out var s))
        {
            var updated = s;
            if (bpm is not null && updated.Bpm != bpm) updated = updated with { Bpm = bpm };
            if (hasStems && !updated.StemsReady) updated = updated with { StemsReady = true };
            if (key is { } k && updated.MusicalKey != k.ToRef()) updated = updated with { MusicalKey = k.ToRef() };
            if (!ReferenceEquals(updated, s)) Replace(i, updated);
        }
        RefreshHarmonyReference();
    }

    private void ApplyAnalysisReport(string filePath, bool busy, string? failure, bool requiredFailure)
    {
        if (!TryFind(filePath, out var i, out var s)) return;
        if (s.IsAnalyzing == busy && s.AnalysisFailure == failure && s.HasRequiredFailure == requiredFailure) return;
        Replace(i, s with { IsAnalyzing = busy, AnalysisFailure = failure, HasRequiredFailure = requiredFailure });
    }

    private void OnBpmMultiplierChosen(string filePath, double multiplier)
    {
        // Also update the matching row so the library list reflects the change.
        if (TryFind(filePath, out var i, out var s) && s.BpmMultiplier != multiplier)
            Replace(i, s with { BpmMultiplier = multiplier });
        var store = _multiplierStore;
        if (store is not null) _ = PersistMultiplierAsync(store, filePath, multiplier);
    }

    private async Task PersistMultiplierAsync(ITempoMultiplierStore store, string filePath, double multiplier)
    {
        try
        {
            await store.PutAsync(filePath, multiplier);
        }
        catch (Exception ex) { Console.WriteLine($"[DB] save bpm override failed: {ex.Message}"); }
    }

    public void ApplyReanalysis(string filePath, double bpm, Key? key)
    {
        if (TryFind(filePath, out var i, out var s))
        {
            var updated = s with { Bpm = bpm };
            if (key is { } k) updated = updated with { MusicalKey = k.ToRef() };
            Replace(i, updated);
        }
        RefreshHarmonyReference();
    }

    public void ReportReanalysisFailure(string filePath, string failure)
    {
        if (TryFind(filePath, out var i, out var s)) Replace(i, s with { AnalysisFailure = failure });
    }

    // ---- Seeding and the harmony reference -----------------------------------------------------

    public void SeedKnownBpms(IReadOnlyDictionary<string, double> bpms)
    {
        for (var i = 0; i < _all.Count; i++)
            if (bpms.TryGetValue(_all[i].FilePath, out var bpm) && _all[i].Bpm != bpm)
                Replace(i, _all[i] with { Bpm = bpm });
    }

    public void SeedKnownBpmMultipliers(IReadOnlyDictionary<string, double> multipliers)
    {
        for (var i = 0; i < _all.Count; i++)
            if (multipliers.TryGetValue(_all[i].FilePath, out var m) && _all[i].BpmMultiplier != m)
                Replace(i, _all[i] with { BpmMultiplier = m });
    }

    public void SeedKnownKeys(IReadOnlyDictionary<string, Key> keys)
    {
        for (var i = 0; i < _all.Count; i++)
            if (keys.TryGetValue(_all[i].FilePath, out var k) && _all[i].MusicalKey != k.ToRef())
                Replace(i, _all[i] with { MusicalKey = k.ToRef() });
        RefreshHarmonyReference();
    }

    public void RefreshHarmonyReference()
    {
        var anchorAnalysis = _decks.Deck1.Analysis.Get<KeyAnalysis>() ?? _decks.Deck2.Analysis.Get<KeyAnalysis>();
        var anchor = anchorAnalysis?.Key;
        if (anchor == HarmonyReferenceKey) return;
        HarmonyReferenceKey = anchor;
        HarmonyReferenceChanged?.Invoke(anchor);
        _publisher.Publish(new Sholto.Data.HarmonyReferenceChanged(
            anchor.ToRef(),
            anchor is { } a ? a.MixableKeys().Select(k => k.ToRef()).ToList() : []));
    }

    // ---- Database services ---------------------------------------------------------------------

    public void AttachServices(ITagService tags, ICrateService crates)
    {
        _tags = tags;
        _crates = crates;
        tags.TagsChanged += async (_, args) =>
        {
            try
            {
                var refreshed = await tags.GetTagsForTrackAsync(args.TrackId, default);
                _appThread.Post(() => ApplyTags(args.TrackId, refreshed));
            }
            catch (Exception ex) { Console.WriteLine($"[Tags] refresh after edit failed: {ex.Message}"); }
        };
        ServicesAttached?.Invoke(tags, crates);
        _publisher.Publish(new LibraryDatabaseAttached(true));
    }

    public void AttachMultiplierStore(ITempoMultiplierStore store) => _multiplierStore = store;

    private void ApplyTags(Guid trackId, IReadOnlyList<string> tags)
    {
        for (var i = 0; i < _all.Count; i++)
        {
            if (_all[i].TrackId != trackId) continue;
            Replace(i, _all[i] with { Tags = tags });
            return;
        }
    }

    // ---- Stem hydration ------------------------------------------------------------------------

    public Task HydrateStemStateAsync()
    {
        var paths = _all.Select(s => s.FilePath).ToArray();  // snapshot so we don't race the catalog
        return Task.Run(() =>
        {
            foreach (var path in paths)
            {
                bool cached;
                try { cached = _stemPresence.Contains(path); }
                catch { cached = false; }
                if (cached) _appThread.Post(() => ApplyStemsReady(path));
            }
        });
    }

    private void ApplyStemsReady(string filePath)
    {
        if (TryFind(filePath, out var i, out var s) && !s.StemsReady) Replace(i, s with { StemsReady = true });
    }
}

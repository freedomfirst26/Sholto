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

/// <summary>See <see cref="ILibrarySession"/>. The catalog, visible rows and selection live in
/// <see cref="ILibraryCatalog"/>; deck and analysis events reach the rows through <see cref="ILibraryDeckWiring"/>.
/// Every mutation happens on the app thread: events from other threads (the analysis reporter, a deck's analysis
/// landing, the tag service) are posted onto it first.</summary>
public sealed class LibrarySession : ILibrarySession
{
    private readonly ITrackScanner _trackScanner;
    private readonly IKeyAnalysisStore _keyStore;
    private readonly IStemPresence _stemPresence;
    private readonly IDecks _decks;
    private readonly ILibraryCatalog _catalog;
    private readonly ILibraryDeckWiring _wiring;
    private readonly ILibrarySeeds _seeds;
    private readonly IAppThread _appThread;
    private readonly IEventPublisher _publisher;
    private readonly ILog _log;
    private readonly ILog _tagsLog;

    private ICrateService? _crates;
    private ITagService? _tags;

    public LibrarySession(
        ITrackScanner trackScanner,
        IKeyAnalysisStore keyStore,
        IStemPresence stemPresence,
        IDecks decks,
        ILibraryCatalog catalog,
        ILibraryDeckWiring wiring,
        ILibrarySeeds seeds,
        IAppThread appThread,
        IEventPublisher publisher,
        ILog log,
        ILog tagsLog)
    {
        _trackScanner = trackScanner;
        _keyStore = keyStore;
        _stemPresence = stemPresence;
        _decks = decks;
        _catalog = catalog;
        _wiring = wiring;
        _seeds = seeds;
        _appThread = appThread;
        _publisher = publisher;
        _log = log;
        _tagsLog = tagsLog;

        _wiring.DeckAnalysisApplied += RefreshHarmonyReference;
    }

    public event Action<IReadOnlyList<TrackSummary>>? RowsChanged
    {
        add => _catalog.RowsChanged += value;
        remove => _catalog.RowsChanged -= value;
    }

    public event Action<TrackSummary>? RowUpdated
    {
        add => _catalog.RowUpdated += value;
        remove => _catalog.RowUpdated -= value;
    }

    public event Action? UnreachableChanged;
    public event Action<Key?>? HarmonyReferenceChanged;
    public event Action<ITagService, ICrateService>? ServicesAttached;
    public event Action? SelectedIndexChanged
    {
        add => _catalog.SelectedIndexChanged += value;
        remove => _catalog.SelectedIndexChanged -= value;
    }

    public IReadOnlyList<TrackSummary> Rows => _catalog.Rows;

    public IReadOnlyList<TrackSummary> Catalog => _catalog.All;

    public ITagService? Tags => _tags;

    public ICrateService? Crates => _crates;

    public string? CurrentDir { get; private set; }

    public bool IsScanning { get; private set; }

    public string? UnreachablePath { get; private set; }

    public bool IsUnreachable => !string.IsNullOrEmpty(UnreachablePath);

    public Key? HarmonyReferenceKey { get; private set; }

    public IBasicAnalysisStore? BasicAnalyses { get; private set; }

    // ---- Scan ----------------------------------------------------------------------------------

    public async Task ScanAsync(string musicDir, LibraryStack? stores)
    {
        if (string.IsNullOrEmpty(musicDir)) return;
        IsScanning = true;
        try
        {
            _log.Info($"scanning {musicDir}");
            var scanned = await _trackScanner.ScanAsync(musicDir);

            Dictionary<string, double>? cachedBpms = null;
            Dictionary<string, double>? cachedMults = null;
            Dictionary<string, Key>? cachedKeys = null;
            Dictionary<string, Guid>? cachedIds = null;
            Dictionary<string, IReadOnlyList<string>>? cachedTagsByPath = null;

            if (stores is not null)
            {
                BasicAnalyses = stores.BasicAnalyses;
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
                    else { missing++; if (missing <= 3) _log.Warn($"no TrackId for: {t.FilePath}"); }
                }
                if (cachedTagsByPath is not null && cachedTagsByPath.TryGetValue(t.FilePath, out var tags))
                    summary = summary with { Tags = tags };
                summaries.Add(summary);
            }
            if (missing > 0)
                _log.Warn($"{missing} row(s) missing TrackId after scan (paths not in the catalog)");

            await _appThread.InvokeAsync(() =>
            {
                _catalog.ReplaceAll(summaries);
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

    public TrackSummary? SummaryFor(string filePath) =>
        _catalog.TryFind(filePath, out _, out var summary) ? summary : null;

    public double GetBpmMultiplierFor(string filePath) =>
        _catalog.TryFind(filePath, out _, out var summary) ? summary.BpmMultiplier : 1.0;

    public Guid TrackIdFor(string filePath) =>
        _catalog.TryFind(filePath, out _, out var summary) ? summary.TrackId : Guid.Empty;

    // ---- Track List and selection --------------------------------------------------------------

    public void ShowTrackList(IReadOnlyList<string> paths) => _catalog.ShowTrackList(paths);

    public int SelectedIndex => _catalog.SelectedIndex;

    public TrackSummary? SelectedSummary => _catalog.SelectedSummary;

    public Track? SelectedTrack => SelectedSummary?.ToTrack();

    public void SetSelectedIndex(int index) => _catalog.SetSelectedIndex(index);

    public void Select(int index) => _catalog.Select(index);

    public void Rotate(int delta) => _catalog.Rotate(delta);

    // ---- Per-track facts -----------------------------------------------------------------------

    public void ApplyReanalysis(string filePath, double bpm, Key? key)
    {
        if (_catalog.TryFind(filePath, out var i, out var s))
        {
            var updated = s with { Bpm = bpm };
            if (key is { } k) updated = updated with { MusicalKey = k.ToRef() };
            _catalog.Replace(i, updated);
        }
        RefreshHarmonyReference();
    }

    public void ReportReanalysisFailure(string filePath, string failure)
    {
        if (_catalog.TryFind(filePath, out var i, out var s)) _catalog.Replace(i, s with { AnalysisFailure = failure });
    }

    // ---- Seeding and the harmony reference -----------------------------------------------------

    public void SeedKnownBpms(IReadOnlyDictionary<string, double> bpms) => _seeds.SeedKnownBpms(bpms);

    public void SeedKnownBpmMultipliers(IReadOnlyDictionary<string, double> multipliers) =>
        _seeds.SeedKnownBpmMultipliers(multipliers);

    public void SeedKnownKeys(IReadOnlyDictionary<string, Key> keys)
    {
        _seeds.SeedKnownKeys(keys);
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
            catch (Exception ex) { _tagsLog.Error($"refresh after edit failed: {ex.Message}"); }
        };
        ServicesAttached?.Invoke(tags, crates);
        _publisher.Publish(new LibraryDatabaseAttached(true));
    }

    public void AttachMultiplierStore(ITempoMultiplierStore store) => _wiring.AttachMultiplierStore(store);

    private void ApplyTags(Guid trackId, IReadOnlyList<string> tags)
    {
        for (var i = 0; i < _catalog.All.Count; i++)
        {
            if (_catalog.All[i].TrackId != trackId) continue;
            _catalog.Replace(i, _catalog.All[i] with { Tags = tags });
            return;
        }
    }

    // ---- Stem hydration ------------------------------------------------------------------------

    public Task HydrateStemStateAsync()
    {
        var paths = _catalog.All.Select(s => s.FilePath).ToArray();  // snapshot so we don't race the catalog
        return Task.Run(() =>
        {
            foreach (var path in paths)
            {
                bool cached;
                try { cached = _stemPresence.TryGet(path) is not null; }
                catch { cached = false; }
                if (cached) _appThread.Post(() => ApplyStemsReady(path));
            }
        });
    }

    public void ApplyStemsReady(string filePath)
    {
        if (_catalog.TryFind(filePath, out var i, out var s) && !s.StemsReady) _catalog.Replace(i, s with { StemsReady = true });
    }
}

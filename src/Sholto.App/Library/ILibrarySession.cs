using Sholto.App.Analysis.Harmony;
using Sholto.App.Library.Crates;
using Sholto.App.Library.Tags;
using Sholto.App.Analysis.Stores;
using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>The music library, headless: scan and catalog, the visible rows (the catalog, then the Track List), the browse selection, per-track facts as they arrive (BPM, multiplier, key, stems, tags, played,
/// analysis progress), the harmony reference key, and the persistence of the BPM multiplier. It emits
/// immutable <see cref="TrackSummary"/> values and change notifications; an interface projects them. All
/// members except the async ones run on the app thread; the async ones hop onto it themselves.
/// <para>It also publishes the same changes on the bus as state events (<c>LibraryRowsChanged</c>, <c>SelectionChanged</c>,
/// <c>LibraryUnreachableChanged</c>, <c>HarmonyReferenceChanged</c>,
/// <c>LibraryDatabaseAttached</c>) and facts (<c>TrackSummaryChanged</c>); those are what the interfaces follow.</para></summary>
public interface ILibrarySession : ITrackSelection
{
    /// <summary>The visible rows, in display order (the catalog until a Track List is shown, then the Track List).</summary>
    IReadOnlyList<TrackSummary> Rows { get; }

    /// <summary>The whole catalog in scan order, whatever is shown. A live list: copy it before using it off the app thread.</summary>
    IReadOnlyList<TrackSummary> Catalog { get; }

    /// <summary>The visible rows were replaced (scan, Track List shown). The payload is a snapshot.</summary>
    event Action<IReadOnlyList<TrackSummary>>? RowsChanged;

    /// <summary>One track's summary changed (BPM landed, tags edited, played, analysis progress...).</summary>
    event Action<TrackSummary>? RowUpdated;

    /// <summary><see cref="UnreachablePath"/> changed.</summary>
    event Action? UnreachableChanged;

    /// <summary>The harmony reference key changed.</summary>
    event Action<Key?>? HarmonyReferenceChanged;

    /// <summary>The database is up: the tag and crate services, for the interface to build its editors over.</summary>
    event Action<ITagService, ICrateService>? ServicesAttached;

    /// <summary>The tag service once the database is up; null before (or without one).</summary>
    ITagService? Tags { get; }

    /// <summary>The crate service once the database is up; null before (or without one).</summary>
    ICrateService? Crates { get; }

    string? CurrentDir { get; }

    bool IsScanning { get; }

    /// <summary>The saved music folder that could not be reached, or null.</summary>
    string? UnreachablePath { get; }

    bool IsUnreachable { get; }

    /// <summary>Camelot key of the harmony anchor: deck 1's loaded key, else deck 2's. Null when neither has one.</summary>
    Key? HarmonyReferenceKey { get; }

    /// <summary>Scan <paramref name="musicDir"/>, upsert into the catalog and hydrate BPMs, multipliers, keys
    /// and tags from <paramref name="stores"/> (null: no database, nothing persisted or hydrated).</summary>
    Task ScanAsync(string musicDir, LibraryStack? stores);

    /// <summary>Remember (or clear, with null) the saved music folder that could not be reached.</summary>
    void SetUnreachablePath(string? path);

    /// <summary>Show exactly these songs, in this order, as the visible rows. Paths the catalog does not hold
    /// are skipped. From the first call on, rows follow this list (a rescan re-applies it); the highlight stays
    /// on the same path, or on the same index (clamped) when that path is gone.</summary>
    void ShowTrackList(IReadOnlyList<string> paths);

    /// <summary>The persisted half/double override for a file; 1.0 when there is none.</summary>
    double GetBpmMultiplierFor(string filePath);

    /// <summary>The catalog id of a file, or <see cref="Guid.Empty"/> when it is not in the catalog.</summary>
    Guid TrackIdFor(string filePath);

    /// <summary>The catalog's summary for a file (not limited to the visible rows), or null when it is not in the catalog.</summary>
    TrackSummary? SummaryFor(string filePath);

    /// <summary>Recompute <see cref="HarmonyReferenceKey"/> from the decks and announce a change.</summary>
    void RefreshHarmonyReference();

    /// <summary>Hydrate stored BPMs into the catalog (startup seeding after the database opens).</summary>
    void SeedKnownBpms(IReadOnlyDictionary<string, double> bpms);

    /// <summary>Hydrate stored half/double overrides into the catalog.</summary>
    void SeedKnownBpmMultipliers(IReadOnlyDictionary<string, double> multipliers);

    /// <summary>Hydrate stored keys into the catalog, then refresh the harmony reference.</summary>
    void SeedKnownKeys(IReadOnlyDictionary<string, Key> keys);

    /// <summary>The database is up: start following tag edits, file new tracks into "All Tracks", and
    /// announce <see cref="ServicesAttached"/>.</summary>
    void AttachServices(ITagService tags, ICrateService crates);

    /// <summary>From now on a BPM multiplier the user chooses on a deck is persisted through
    /// <paramref name="store"/>. Before this (database unavailable) it is applied to the catalog only.</summary>
    void AttachMultiplierStore(ITempoMultiplierStore store);

    /// <summary>A forced re-analysis finished: store its BPM (and key, if any) on the track and refresh the
    /// harmony reference.</summary>
    void ApplyReanalysis(string filePath, double bpm, Key? key);

    /// <summary>A forced re-analysis threw: show <paramref name="failure"/> on the track.</summary>
    void ReportReanalysisFailure(string filePath, string failure);

    /// <summary>The track's stems exist (a re-analysis separated them, or found them cached): tick the row.</summary>
    void ApplyStemsReady(string filePath);

    /// <summary>Walk the catalog and mark the tracks whose four stem files already exist. Runs off the app
    /// thread (each check hashes 1 MiB of the file); never triggers analysis.</summary>
    Task HydrateStemStateAsync();
}

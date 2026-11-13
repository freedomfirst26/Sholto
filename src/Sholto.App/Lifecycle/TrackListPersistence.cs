using Sholto.App.Library;
using Sholto.App.Library.Crates;
using Sholto.Data;
using System.Text.Json;

namespace Sholto.App.Lifecycle;

/// <summary>See <see cref="ITrackListPersistence"/>: restores the saved Track List, saves each change, migrates
/// the retired shortlist once, and loads All Tracks on a first launch. The restore task is awaited by the first
/// scan, which loads All Tracks when there was no saved list at all (first launch); a saved empty list stays empty.</summary>
public sealed class TrackListPersistence(
    ILibraryDatabase database,
    ILibrarySession library,
    ISettingPreference trackListPreference,
    ISettingPreference legacyShortlistPreference,
    ITrackList trackList,
    ISavedTrackListCodec trackListCodec,
    IAppThread appThread,
    ILog log,
    ILog shortlistLog) : ITrackListPersistence
{
    private readonly ILibraryDatabase _database = database;
    private readonly ILibrarySession _library = library;
    private readonly ISettingPreference _trackListPreference = trackListPreference;
    private readonly ISettingPreference _legacyShortlistPreference = legacyShortlistPreference;
    private readonly ITrackList _trackList = trackList;
    private readonly ISavedTrackListCodec _trackListCodec = trackListCodec;
    private readonly IAppThread _appThread = appThread;
    private readonly ILog _log = log;
    private readonly ILog _shortlistLog = shortlistLog;

    // True once the saved list was restored on a database that opened: from then on a change is saved.
    private bool _persisting;
    // True when the list changed before saving was switched on; that change is saved once it is.
    private bool _changedBeforePersisting;
    private bool _trackListFirstLaunch;
    private Task _trackListRestored = Task.CompletedTask;

    public void Start()
    {
        // Restore the Track List; a later change is saved once that is done.
        _trackList.Changed += OnTrackListChanged;
        _trackListRestored = Task.Run(RestoreTrackListAsync);
    }

    private async Task RestoreTrackListAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var saved = await _trackListPreference.GetAsync();
        List<TrackListEntry>? entries = null;
        List<TrackListSource>? sources = null;
        if (saved is null)
            _trackListFirstLaunch = true;
        else if (ParseTrackList(saved) is { } list)
        {
            entries = list.Entries.Select(e => new TrackListEntry(e.Path, e.SourceKeys)).ToList();
            sources = list.Sources.Select(s => new TrackListSource(s.Key, s.Kind, s.Name, 0)).ToList();
        }

        // Restore and switch saving on in one app-thread step, so no change can land between the two unsaved.
        // A change made before this (a song added while starting up) was merged by the restore: save it now.
        await _appThread.InvokeAsync(() =>
        {
            var changedBeforeRestore = _changedBeforePersisting;
            if (entries is not null && sources is not null) _trackList.Restore(entries, sources);
            _persisting = true;
            if (changedBeforeRestore) Save();
        });
        await MigrateShortlistAsync();
    }

    // One time: the retired shortlist's songs join the end of the Track List as songs (de-duplicated by the
    // list), then the saved shortlist is emptied so no later startup adds them again.
    private async Task MigrateShortlistAsync()
    {
        var paths = ParseLegacyShortlist(await _legacyShortlistPreference.GetAsync());
        if (paths.Count == 0) return;

        await _appThread.InvokeAsync(() =>
        {
            foreach (var path in paths)
                _trackList.Handle(new LoadSongToTrackList(path, _migrationOrigin));
        });
        await _legacyShortlistPreference.SetAsync("[]");
    }

    private readonly Origin _migrationOrigin = new(InterfaceIds.MainUI, "startup", "shortlist-migration");

    private IReadOnlyList<string> ParseLegacyShortlist(string? saved)
    {
        if (string.IsNullOrEmpty(saved)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(saved) ?? [];
        }
        catch (JsonException ex)
        {
            _shortlistLog.Warn($"saved list unreadable, not migrated: {ex.Message}");
            return [];
        }
    }

    private SavedTrackList? ParseTrackList(string saved)
    {
        try
        {
            return _trackListCodec.Decode(saved);
        }
        catch (JsonException ex)
        {
            _log.Warn($"saved list unreadable, starting empty: {ex.Message}");
            return null;
        }
    }

    // Raised on the app thread: snapshot the list now, write on the pool.
    private void OnTrackListChanged()
    {
        if (!_persisting)
        {
            _changedBeforePersisting = true;
            return;
        }
        Save();
    }

    private void Save()
    {
        var json = _trackListCodec.Encode(new SavedTrackList(
            _trackList.Entries.Select(e => new SavedTrackListEntry(e.Path, [.. e.SourceKeys])).ToList(),
            _trackList.Sources.Select(s => new SavedTrackListSource(s.Key, s.Kind, s.Name)).ToList()));
        _ = Task.Run(async () =>
        {
            try
            {
                await _trackListPreference.SetAsync(json);
            }
            catch (Exception ex) { _log.Error($"persist failed: {ex.Message}"); }
        });
    }

    // First launch: once the first scan is done and the new songs are filed, load the "All Tracks" crate.
    public async Task LoadAllTracksOnFirstLaunchAsync()
    {
        await _trackListRestored;
        var crates = _library.Crates;
        if (!_trackListFirstLaunch || crates is null) return;
        _trackListFirstLaunch = false;
        try
        {
            var crateId = await crates.CreateAsync(CrateNames.AllTracks);
            // The scan files songs into the crate in the background: wait until it holds the whole catalog.
            var catalogIds = await _appThread.InvokeAsync(() => _library.Catalog.Select(s => s.TrackId).Where(id => id != Guid.Empty).ToHashSet());
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var members = await crates.TrackIdsAsync(crateId);
                if (catalogIds.IsSubsetOf(members)) break;
                await Task.Delay(100);
            }
            await _appThread.InvokeAsync(() => _trackList.Handle(new LoadCrateToTrackList(crateId, CrateNames.AllTracks, _firstLaunchOrigin)));
        }
        catch (Exception ex) { _log.Error($"first-launch load failed: {ex.Message}"); }
    }

    private readonly Origin _firstLaunchOrigin = new(InterfaceIds.MainUI, "startup", "first-launch");
}

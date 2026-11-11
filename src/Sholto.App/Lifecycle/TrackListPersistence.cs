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
    IAppThread appThread) : ITrackListPersistence
{
    private readonly ILibraryDatabase _database = database;
    private readonly ILibrarySession _library = library;
    private readonly ISettingPreference _trackListPreference = trackListPreference;
    private readonly ISettingPreference _legacyShortlistPreference = legacyShortlistPreference;
    private readonly ITrackList _trackList = trackList;
    private readonly ISavedTrackListCodec _trackListCodec = trackListCodec;
    private readonly IAppThread _appThread = appThread;

    // True once the saved list was restored on a database that opened: from then on a change is saved.
    private bool _persisting;
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
        if (saved is null)
            _trackListFirstLaunch = true;
        else if (ParseTrackList(saved) is { } list)
        {
            var entries = list.Entries.Select(e => new TrackListEntry(e.Path, e.SourceKeys)).ToList();
            var sources = list.Sources.Select(s => new TrackListSource(s.Key, s.Kind, s.Name, 0)).ToList();
            await _appThread.InvokeAsync(() => _trackList.Restore(entries, sources));
        }

        _persisting = true;
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
            Console.WriteLine($"[Shortlist] saved list unreadable, not migrated: {ex.Message}");
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
            Console.WriteLine($"[TrackList] saved list unreadable, starting empty: {ex.Message}");
            return null;
        }
    }

    // Raised on the app thread: snapshot the list now, write on the pool.
    private void OnTrackListChanged()
    {
        if (!_persisting) return;
        var json = _trackListCodec.Encode(new SavedTrackList(
            _trackList.Entries.Select(e => new SavedTrackListEntry(e.Path, [.. e.SourceKeys])).ToList(),
            _trackList.Sources.Select(s => new SavedTrackListSource(s.Key, s.Kind, s.Name)).ToList()));
        _ = Task.Run(async () =>
        {
            try
            {
                await _trackListPreference.SetAsync(json);
            }
            catch (Exception ex) { Console.WriteLine($"[TrackList] persist failed: {ex.Message}"); }
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
        catch (Exception ex) { Console.WriteLine($"[TrackList] first-launch load failed: {ex.Message}"); }
    }

    private readonly Origin _firstLaunchOrigin = new(InterfaceIds.MainUI, "startup", "first-launch");
}

using Sholto.Data;

namespace Sholto.App.Library;

/// <inheritdoc cref="ITrackList"/>
public sealed class TrackList : ITrackList
{
    private const string SongsKey = "songs";

    private readonly ILibrarySession _library;
    private readonly IAppThread _appThread;
    private readonly IEventPublisher _publisher;
    private readonly List<TrackListEntry> _entries = [];
    private readonly Dictionary<string, TrackListEntry> _byPath = [];
    private readonly List<SourceInfo> _sources = [];

    public TrackList(ILibrarySession library, IAppThread appThread, IEventPublisher publisher)
    {
        _library = library;
        _appThread = appThread;
        _publisher = publisher;
        Announce();
    }

    private sealed record SourceInfo(string Key, TrackListSourceKind Kind, string Name);

    public event Action? Changed;

    public IReadOnlyList<TrackListEntry> Entries => _entries;

    public IReadOnlyList<TrackListSource> Sources => BuildSources();

    public void Handle(in LoadSongToTrackList command)
    {
        // A song already in the list changes nothing, whichever source brought it.
        if (_byPath.ContainsKey(command.Path)) return;
        if (Add(SongsKey, TrackListSourceKind.Songs, "Songs", [command.Path])) Commit();
    }

    public void Handle(in LoadCrateToTrackList command) => _ = LoadCrateAsync(command.CrateId, command.Name);

    public void Handle(in LoadTagToTrackList command) => _ = LoadTagAsync(command.Tag);

    public void Handle(in RemoveFromTrackList command)
    {
        if (!_byPath.Remove(command.Path, out var entry)) return;
        _entries.Remove(entry);
        Commit();
    }

    public void Handle(in RemoveSourceFromTrackList command)
    {
        var changed = false;
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (!entry.SourceKeys.Remove(command.SourceKey)) continue;
            changed = true;
            if (entry.SourceKeys.Count > 0) continue;
            _entries.RemoveAt(i);
            _byPath.Remove(entry.Path);
        }
        var key = command.SourceKey;
        changed |= _sources.RemoveAll(s => s.Key == key) > 0;
        if (changed) Commit();
    }

    public void Handle(in MoveInTrackList command)
    {
        if (!_byPath.TryGetValue(command.Path, out var entry)) return;
        var from = _entries.IndexOf(entry);
        var to = Math.Clamp(command.ToIndex, 0, _entries.Count - 1);
        if (from == to) return;
        _entries.RemoveAt(from);
        _entries.Insert(to, entry);
        Commit();
    }

    public void Handle(in ClearTrackList command)
    {
        _entries.Clear();
        _byPath.Clear();
        _sources.Clear();
        Commit();
    }

    public void Restore(IReadOnlyList<TrackListEntry> entries, IReadOnlyList<TrackListSource> sources)
    {
        var earlier = _entries.ToList();
        var earlierSources = _sources.ToList();
        _entries.Clear();
        _byPath.Clear();
        _sources.Clear();
        foreach (var source in sources) RegisterSource(source.Key, source.Kind, source.Name);
        foreach (var entry in entries) Merge(entry.Path, entry.SourceKeys);
        foreach (var source in earlierSources) RegisterSource(source.Key, source.Kind, source.Name);
        foreach (var entry in earlier) Merge(entry.Path, entry.SourceKeys);
        _library.ShowTrackList(Paths());
        Announce();
    }

    private async Task LoadCrateAsync(int crateId, string name)
    {
        var crates = _library.Crates;
        if (crates is null) return;
        try
        {
            // The catalog is a live list the app thread owns: snapshot it here, before the await.
            var pathById = PathsById();
            var ids = await crates.TrackIdsAsync(crateId);
            var paths = new List<string>(ids.Count);
            foreach (var id in ids)
                if (pathById.TryGetValue(id, out var path)) paths.Add(path);
            await _appThread.InvokeAsync(() => AddAndCommit($"crate:{crateId}", TrackListSourceKind.Crate, name, paths));
        }
        catch (Exception ex) { Console.WriteLine($"[TrackList] load crate failed: {ex.Message}"); }
    }

    private async Task LoadTagAsync(string tag)
    {
        var tags = _library.Tags;
        if (tags is null) return;
        try
        {
            var catalog = _library.Catalog.ToArray();
            var ids = new HashSet<Guid>(await tags.GetTrackIdsForTagAsync(tag, default));
            var paths = catalog.Where(s => ids.Contains(s.TrackId)).Select(s => s.FilePath).ToList();
            await _appThread.InvokeAsync(() => AddAndCommit($"tag:{tag.ToLowerInvariant()}", TrackListSourceKind.Tag, tag, paths));
        }
        catch (Exception ex) { Console.WriteLine($"[TrackList] load tag failed: {ex.Message}"); }
    }

    private Dictionary<Guid, string> PathsById()
    {
        var map = new Dictionary<Guid, string>();
        foreach (var summary in _library.Catalog)
            if (summary.TrackId != Guid.Empty) map[summary.TrackId] = summary.FilePath;
        return map;
    }

    private void AddAndCommit(string key, TrackListSourceKind kind, string name, IReadOnlyList<string> paths)
    {
        if (Add(key, kind, name, paths)) Commit();
    }

    /// <summary>Append the paths as coming from one source; true when anything changed.</summary>
    private bool Add(string key, TrackListSourceKind kind, string name, IReadOnlyList<string> paths)
    {
        var changed = RegisterSource(key, kind, name);
        foreach (var path in paths) changed |= Merge(path, [key]);
        return changed;
    }

    private bool RegisterSource(string key, TrackListSourceKind kind, string name)
    {
        if (_sources.Any(s => s.Key == key)) return false;
        _sources.Add(new SourceInfo(key, kind, name));
        return true;
    }

    private bool Merge(string path, IEnumerable<string> keys)
    {
        if (!_byPath.TryGetValue(path, out var entry))
        {
            entry = new TrackListEntry(path, keys);
            _entries.Add(entry);
            _byPath[path] = entry;
            return true;
        }
        var changed = false;
        foreach (var key in keys) changed |= entry.SourceKeys.Add(key);
        return changed;
    }

    private List<string> Paths() => _entries.Select(e => e.Path).ToList();

    private List<TrackListSource> BuildSources()
    {
        var sources = new List<TrackListSource>(_sources.Count);
        foreach (var source in _sources)
        {
            var count = _entries.Count(e => e.SourceKeys.Contains(source.Key));
            if (count > 0) sources.Add(new TrackListSource(source.Key, source.Kind, source.Name, count));
        }
        return sources;
    }

    private void Commit()
    {
        _library.ShowTrackList(Paths());
        Announce();
        Changed?.Invoke();
    }

    private void Announce() => _publisher.Publish(new TrackListChanged(BuildSources(), _entries.Count) { Paths = Paths() });
}

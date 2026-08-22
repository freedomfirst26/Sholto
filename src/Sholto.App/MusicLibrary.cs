using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Sholto.Analysis;
using Sholto.App.ViewModels;
using Sholto.Music;
using Sholto.Storage;
using Sholto.Storage.Entities;
using EfTrack = Sholto.Storage.Entities.Track;
using LibTrack = Sholto.Music.Track;

namespace Sholto.App;

public sealed class MusicLibrary : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<TrackRow> Tracks { get; } = new();
    public event Action<string>? Scanned;

    private string? _currentDir;
    public string? CurrentDir
    {
        get => _currentDir;
        private set { if (_currentDir == value) return; _currentDir = value; Notify(); }
    }

    private string? _unreachablePath;
    public string? UnreachablePath
    {
        get => _unreachablePath;
        set
        {
            if (_unreachablePath == value) return;
            _unreachablePath = value;
            Notify();
            Notify(nameof(IsUnreachable));
        }
    }
    public bool IsUnreachable => !string.IsNullOrEmpty(_unreachablePath);

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        private set { if (_isScanning == value) return; _isScanning = value; Notify(); }
    }

    private string? _activeTagFilter;
    public string? ActiveTagFilter
    {
        get => _activeTagFilter;
        private set { if (_activeTagFilter == value) return; _activeTagFilter = value; Notify(); }
    }

    private List<TrackRow>? _unfilteredSnapshot;

    public void ApplyTagFilter(string tag, IReadOnlyCollection<Guid> trackIds)
    {
        _unfilteredSnapshot ??= Tracks.ToList();
        var allowed = new HashSet<Guid>(trackIds);
        Tracks.Clear();
        foreach (var row in _unfilteredSnapshot)
            if (allowed.Contains(row.TrackId)) Tracks.Add(row);
        ActiveTagFilter = tag;
    }

    /// <summary>Filter the library down to a crate's tracks. Reuses the same
    /// snapshot mechanism as the tag filter; <see cref="ClearTagFilter"/> clears it.
    /// The label carries a 📦 so the active-filter chip reads as a crate, not a tag.</summary>
    public void ApplyCrateFilter(string crateName, IReadOnlyCollection<Guid> trackIds)
        => ApplyTagFilter($"📦 {crateName}", trackIds);

    public void ClearTagFilter()
    {
        if (_unfilteredSnapshot is null) return;
        Tracks.Clear();
        foreach (var row in _unfilteredSnapshot) Tracks.Add(row);
        _unfilteredSnapshot = null;
        ActiveTagFilter = null;
    }

    public async Task ScanAsync(string musicDir, IDbContextFactory<SholtoDbContext>? factory)
    {
        if (string.IsNullOrEmpty(musicDir)) return;
        ClearTagFilter();
        IsScanning = true;
        try
        {
            Console.WriteLine($"[Library] scanning {musicDir}");
            var scanned = await TrackScanner.ScanAsync(musicDir);

            Dictionary<string, double>? cachedBpms = null;
            Dictionary<string, double>? cachedMults = null;
            Dictionary<string, string>? cachedKeys = null;
            Dictionary<string, Guid>? cachedIds = null;
            Dictionary<string, IReadOnlyList<string>>? cachedTagsByPath = null;

            if (factory is not null)
            {
                await using var db = factory.CreateDbContext();

                foreach (var t in scanned)
                {
                    var info = new FileInfo(t.FilePath);
                    if (!info.Exists) continue;
                    long size = info.Length;
                    long mtime = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds();

                    var existing = await db.Tracks.FirstOrDefaultAsync(x => x.Path == t.FilePath);
                    if (existing is null)
                    {
                        db.Tracks.Add(new EfTrack
                        {
                            Path = t.FilePath,
                            Title = t.Title,
                            Artist = t.Artist,
                            FileSize = size,
                            FileMtime = mtime,
                            DurationSecs = t.Duration.TotalSeconds,
                        });
                    }
                    else
                    {
                        existing.Title = t.Title;
                        existing.Artist = t.Artist;
                        existing.FileSize = size;
                        existing.FileMtime = mtime;
                        existing.DurationSecs = t.Duration.TotalSeconds;
                    }
                }
                await db.SaveChangesAsync();

                var pathToId = await db.Tracks.AsNoTracking()
                    .Select(t => new { t.Path, t.Id })
                    .ToDictionaryAsync(x => x.Path, x => x.Id);
                cachedIds = pathToId;

                var tagsByTrackId = await db.TrackTags.AsNoTracking()
                    .OrderBy(tt => tt.Tag.Name)
                    .Select(tt => new { tt.TrackId, tt.Tag.Name })
                    .ToListAsync();
                var tagsGrouped = tagsByTrackId
                    .GroupBy(x => x.TrackId)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name).ToList());

                cachedTagsByPath = pathToId.ToDictionary(
                    kv => kv.Key,
                    kv => tagsGrouped.TryGetValue(kv.Value, out var tags)
                        ? tags
                        : (IReadOnlyList<string>)Array.Empty<string>());

                var basicRows = await db.BasicAnalyses.AsNoTracking()
                    .Select(b => new { b.Track.Path, b.Data }).ToListAsync();
                cachedBpms = new();
                foreach (var r in basicRows)
                {
                    var basic = AnalysisCodec.Decode(r.Data);
                    if (basic is not null) cachedBpms[r.Path] = basic.Bpm;
                }

                cachedMults = await db.BpmOverrides.AsNoTracking()
                    .Select(o => new { o.Track.Path, o.Multiplier })
                    .ToDictionaryAsync(x => x.Path, x => x.Multiplier);

                var keyRows = await db.KeyAnalyses.AsNoTracking()
                    .Select(k => new { k.Track.Path, k.Data }).ToListAsync();
                cachedKeys = new();
                foreach (var r in keyRows)
                {
                    var key = KeyAnalysisCodec.Decode(r.Data);
                    if (key is not null && !string.IsNullOrEmpty(key.Camelot)) cachedKeys[r.Path] = key.Camelot;
                }
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Tracks.Clear();
                foreach (var t in scanned) Tracks.Add(new TrackRow(t));

                if (cachedBpms is not null)
                    foreach (var row in Tracks)
                        if (cachedBpms.TryGetValue(row.FilePath, out var bpm)) row.Bpm = bpm;
                if (cachedMults is not null)
                    foreach (var row in Tracks)
                        if (cachedMults.TryGetValue(row.FilePath, out var m)) row.BpmMultiplier = m;
                if (cachedKeys is not null)
                    foreach (var row in Tracks)
                        if (cachedKeys.TryGetValue(row.FilePath, out var k)) row.Key = k;
                if (cachedIds is not null)
                {
                    int missing = 0;
                    foreach (var row in Tracks)
                    {
                        if (cachedIds.TryGetValue(row.FilePath, out var id)) row.TrackId = id;
                        else { missing++; if (missing <= 3) Console.WriteLine($"[Library] no TrackId for: {row.FilePath}"); }
                    }
                    if (missing > 0)
                        Console.WriteLine($"[Library] {missing} row(s) missing TrackId after scan (paths not in EF Tracks)");
                }
                if (cachedTagsByPath is not null)
                    foreach (var row in Tracks)
                        if (cachedTagsByPath.TryGetValue(row.FilePath, out var tags)) row.Tags = tags;

                CurrentDir = musicDir;
                UnreachablePath = null;
            });

            Scanned?.Invoke(musicDir);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.App.ViewModels;
using Sholto.Library;
using Sholto.Storage;

namespace Sholto.App;

/// <summary>
/// The user's music library. Owns the live <see cref="Tracks"/> collection,
/// tracks which directory it last scanned, and exposes "is the source path
/// reachable?" status so the UI can surface a banner when an external drive
/// is unmounted.
///
/// Event-based: callers subscribe to <see cref="Scanned"/> to react to a
/// successful scan (e.g. for hydrating analyses or refreshing UI bits). The
/// MainViewModel observes Tracks via INotifyCollectionChanged on the
/// ObservableCollection.
/// </summary>
public sealed class MusicLibrary : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Live track rows. UI binds directly to this. Mutated only on the
    /// UI thread by <see cref="ScanAsync"/>.</summary>
    public ObservableCollection<TrackRow> Tracks { get; } = new();

    /// <summary>Fires after every successful scan, on the UI thread, after
    /// <see cref="Tracks"/> has been replaced. Carries the path that was
    /// scanned. <see cref="MainViewModel"/> subscribes to hydrate per-row
    /// state (BPMs, keys, multipliers, stems-ready) against the new rows.</summary>
    public event Action<string>? Scanned;

    private string? _currentDir;
    /// <summary>The directory that was last successfully scanned, or null if
    /// nothing has been loaded yet.</summary>
    public string? CurrentDir
    {
        get => _currentDir;
        private set { if (_currentDir == value) return; _currentDir = value; Notify(); }
    }

    private string? _unreachablePath;
    /// <summary>Non-null when a music dir is saved but its path doesn't exist
    /// on disk right now (typical cause: external drive not mounted). UI shows
    /// a banner pointing at this path with a "Choose folder…" re-pick button.</summary>
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

    /// <summary>
    /// Scan <paramref name="musicDir"/> and replace <see cref="Tracks"/> with
    /// whatever's there. Hydrates cached BPMs / keys / overrides from
    /// <paramref name="db"/> (if non-null) onto the new rows before the
    /// <see cref="Scanned"/> event fires.
    /// Safe to call from any thread; marshals UI updates to the dispatcher.
    /// </summary>
    public async Task ScanAsync(string musicDir, SholtoDatabase? db)
    {
        if (string.IsNullOrEmpty(musicDir)) return;
        IsScanning = true;
        try
        {
            Console.WriteLine($"[Library] scanning {musicDir}");
            var tracks = await TrackScanner.ScanAsync(musicDir);

            Dictionary<string, double>? cachedBpms = null;
            Dictionary<string, double>? cachedMults = null;
            Dictionary<string, string>? cachedKeys = null;
            if (db is not null)
            {
                foreach (var t in tracks) await db.UpsertTrackAsync(t);
                cachedBpms  = await db.GetAllBpmsAsync();
                cachedMults = await db.GetAllBpmMultipliersAsync();
                cachedKeys  = await db.GetAllKeysAsync();
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Tracks.Clear();
                foreach (var t in tracks) Tracks.Add(new TrackRow(t));

                if (cachedBpms is not null)
                    foreach (var row in Tracks)
                        if (cachedBpms.TryGetValue(row.FilePath, out var bpm)) row.Bpm = bpm;
                if (cachedMults is not null)
                    foreach (var row in Tracks)
                        if (cachedMults.TryGetValue(row.FilePath, out var m)) row.BpmMultiplier = m;
                if (cachedKeys is not null)
                    foreach (var row in Tracks)
                        if (cachedKeys.TryGetValue(row.FilePath, out var k)) row.Key = k;

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

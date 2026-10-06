using Microsoft.Data.Sqlite;
using Sholto.App.Library;
using Sholto.App.Library.Crates;
using Sholto.App.Library.Tags;
using Sholto.App.Storage;

namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>
/// <see cref="IDemoLibrary"/> over the real Sqlite storage, in a temp file (never the user's library). Tracks are picked
/// by their position in the scanned catalogue (artist, then title), so "Peak time" plus "Vocal" leaves several of them.
/// </summary>
/// <param name="storage">Opens the database.</param>
public sealed class DemoLibrary(SholtoStorage storage) : IDemoLibrary, IDisposable
{
    /// <summary>Crates, created in this order so the newest (listed first) is "Peak time"; each lists catalogue positions.</summary>
    private readonly (string Name, int[] Tracks)[] Crates =
    [
        ("Afterhours", []),
        ("Featurecast", [4, 9, 13, 14]),
        ("Warm-up", [1, 2, 6, 8, 11, 12, 15, 16, 17]),
        ("Peak time", [0, 1, 3, 4, 5, 7, 9, 10, 13]),
    ];

    /// <summary>Tags, with the catalogue positions wearing each; "Vocal" has the most so it heads the rail.</summary>
    private readonly (string Name, int[] Tracks)[] Tags =
    [
        ("Vocal", [0, 2, 3, 5, 7, 8, 10, 12, 14, 16]),
        ("Melodic", [1, 2, 4, 6, 8, 11, 13, 15, 17]),
        ("Driving", [0, 1, 3, 5, 9, 13]),
        ("Dark", [4, 9, 10, 14]),
        ("Industrial", [3, 9]),
        ("Funk", [12, 16]),
        ("Goth", [9]),
    ];

    private readonly SholtoStorage _storage = storage;
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sholto-harness-{Guid.NewGuid():N}.db");
    private DatabaseStack? _database;

    /// <summary>Releases the pooled connections, then deletes the temp database and its <c>-wal</c>/<c>-shm</c> side files.
    /// A file that cannot be deleted is left behind: cleanup never fails the harness.</summary>
    public void Dispose()
    {
        // The stack's context pool is not exposed (no dispose on DatabaseStack); clearing the Sqlite pool closes the connections.
        try { SqliteConnection.ClearAllPools(); }
        catch (Exception) { }
        _database = null;
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try { File.Delete(path); }
            catch (Exception) { }
        }
    }

    public async Task<LibraryStack> OpenAsync()
    {
        _database ??= await _storage.OpenAsync(_dbPath);
        return new LibraryStack(_database.Tracks, _database.Tags, _database.BasicAnalyses, _database.TempoMultipliers);
    }

    public async Task SeedAsync(ILibrarySession library)
    {
        await OpenAsync();
        var database = _database!;
        // Attached first, as in the app, so a tag added below reaches the rows through the library's own refresh.
        library.AttachServices(database.Tags, database.Crates);

        var catalogue = library.Catalog.ToArray();
        foreach (var (name, positions) in Crates)
        {
            var crateId = await database.Crates.CreateAsync(name);
            foreach (var position in positions.Where(p => p < catalogue.Length))
                await database.Crates.AddTrackAsync(crateId, catalogue[position].TrackId);
        }
        foreach (var (name, positions) in Tags)
            foreach (var position in positions.Where(p => p < catalogue.Length))
                await database.Tags.AddTagAsync(catalogue[position].TrackId, name, default);
    }
}

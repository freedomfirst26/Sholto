using Sholto.App.Storage;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>Opens the library database through <see cref="ISholtoStorage"/> and publishes
/// the result to everything awaiting <see cref="Opened"/>.</summary>
public sealed class LibraryDatabase(ISholtoStorage storage, ILogFactory logs) : ILibraryDatabase
{
    private readonly ISholtoStorage _storage = storage;
    private readonly ILogFactory _logs = logs;
    private readonly ILog _log = logs.Create("DB");
    // Other startup tasks (music-dir resolution, audio init) need the DB to read
    // settings. They await this so they don't race the DB open task.
    private readonly TaskCompletionSource<DatabaseStack?> _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DatabaseStack? _database;

    public Task<DatabaseStack?> Opened => _ready.Task;

    public LibraryStack? Stores =>
        _database is null ? null : new LibraryStack(_database.Tracks, _database.Tags, _database.BasicAnalyses, _database.TempoMultipliers);

    public async Task OpenAsync(Func<DatabaseStack, Task> onOpened)
    {
        try
        {
            var database = await _storage.OpenAsync();
            _database = database;
            _log.Info($"opened {_storage.DefaultDbPath()}");

            await onOpened(database);
        }
        catch (Exception ex)
        {
            _log.Error($"failed to open: {ex.Message}");
        }
        finally
        {
            _ready.TrySetResult(_database);
        }
    }
}

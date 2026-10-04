using Sholto.App.Storage;
using Sholto.App.Library;

namespace Sholto.App.Lifecycle;

/// <summary>
/// The library database as the composition root sees it: opened once, off the UI thread,
/// after the first paint, and awaited by everything that needs settings or stores.
/// <see cref="Opened"/> completes exactly once, with the database or with null if the
/// open (or the <c>onOpened</c> work) failed — it never faults.
/// </summary>
public interface ILibraryDatabase
{
    /// <summary>Completes after <see cref="OpenAsync"/> has finished, including its
    /// <c>onOpened</c> work: the opened database, or null when opening failed.</summary>
    Task<DatabaseStack?> Opened { get; }

    /// <summary>The scan stores over the opened database, or null before it opened or
    /// when opening failed.</summary>
    LibraryStack? Stores { get; }

    /// <summary>Opens the database, logs, then runs <paramref name="onOpened"/> with it
    /// (still before <see cref="Opened"/> completes). Any failure is logged, not thrown;
    /// <see cref="Opened"/> is completed in every case.</summary>
    Task OpenAsync(Func<DatabaseStack, Task> onOpened);
}

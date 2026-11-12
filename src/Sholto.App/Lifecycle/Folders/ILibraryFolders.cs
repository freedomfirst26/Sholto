using Sholto.Data;

namespace Sholto.App.Lifecycle.Folders;

/// <summary>The music folder and the analysis folder (where stems go): which ones to use at startup, the
/// one question put to the user about them, and applying the answer. Publishes
/// <see cref="FoldersNeeded"/> and waits for <see cref="ChooseFolders"/> (both paths null means
/// cancelled). Every non-null answer ends in exactly one <see cref="AnalysisFolderChanged"/> or a fresh
/// <see cref="FoldersNeeded"/> carrying the problem. The saved analysis folder is never created at
/// startup: only a validated choice creates it.</summary>
public interface ILibraryFolders :
    ICommandHandler<ChooseFolders>,
    ICommandHandler<ChangeFolders>,
    IQueryHandler<CheckFolders, Task<FolderCheckResult>>
{
    /// <summary>Settle both folders at startup, asking the user when a row needs it. Returns the music
    /// folder to scan, or null when there is none to scan (cancelled, or unreachable). Call off the app
    /// thread, once the database is opening.</summary>
    Task<string?> ResolveAsync();
}

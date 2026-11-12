using Sholto.Data;

namespace Sholto.App.Lifecycle.Folders;

/// <summary>Folders that do nothing, for a host with no library and no pickers (Bench): nothing is asked,
/// nothing is scanned, every command is accepted and ignored, and every check passes.</summary>
public sealed class NullLibraryFolders : ILibraryFolders
{
    public Task<string?> ResolveAsync() => Task.FromResult<string?>(null);

    public void Handle(in ChooseFolders command) { }

    public void Handle(in ChangeFolders command) { }

    public Task<FolderCheckResult> Handle(in CheckFolders query) =>
        Task.FromResult(new FolderCheckResult(null, null, null));
}

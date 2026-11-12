namespace Sholto.Data;

/// <summary>Whether the music and analysis folders are acceptable together (see <see cref="FolderCheckResult"/>).
/// Either path may be null while the user has not picked it yet. Changes nothing.</summary>
public readonly record struct CheckFolders(string? Music, string? Analysis) : IQuery<Task<FolderCheckResult>>;

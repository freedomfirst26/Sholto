using Sholto.Data;

namespace Sholto.App.Lifecycle.Folders;

/// <summary>Decides whether a music folder and an analysis folder are acceptable together. Reads the disk
/// (existence, a write probe) but creates nothing that outlives the call. The analysis folder must stay out
/// of the music folder, since the library scan takes every <c>.wav</c> under it and would import the
/// stems as tracks.</summary>
public interface IFolderCheck
{
    /// <summary>Each folder's problem (null when it is fine; a null path has none) and the analysis folder to
    /// suggest for <paramref name="music"/>.</summary>
    FolderCheckResult Check(string? music, string? analysis);

    /// <summary>The analysis folder to offer for <paramref name="music"/>: <c>&lt;parent of music&gt;/&lt;FolderName&gt;</c>.
    /// Null when there is no music folder, or its parent is the filesystem root or cannot be written to.</summary>
    string? SuggestFor(string? music);

    /// <summary>The absolute form of <paramref name="path"/> with no trailing separator, for comparing folders.</summary>
    string Normalize(string path);
}

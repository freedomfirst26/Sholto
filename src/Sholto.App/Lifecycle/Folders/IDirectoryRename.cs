namespace Sholto.App.Lifecycle.Folders;

/// <summary>Renames a directory in one step, the way <see cref="Directory.Move(string, string)"/> does. A seam,
/// so a test can simulate a rename across drives.</summary>
public interface IDirectoryRename
{
    /// <summary>Rename <paramref name="from"/> to <paramref name="to"/>. False, with nothing changed, when it
    /// cannot be done in one step: the two are on different filesystems (the rename fails with an
    /// <see cref="IOException"/>), the source is missing or the target already exists.</summary>
    bool TryRename(string from, string to);
}

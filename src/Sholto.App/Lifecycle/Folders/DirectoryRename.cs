namespace Sholto.App.Lifecycle.Folders;

/// <inheritdoc />
/// <remarks>On Linux <see cref="Directory.Move(string, string)"/> is <c>rename(2)</c>, so a move across
/// filesystems (EXDEV) surfaces as an <see cref="IOException"/>. Any such failure answers false: the caller
/// falls back to copying, which is safe whatever the cause.</remarks>
public sealed class DirectoryRename : IDirectoryRename
{
    public bool TryRename(string from, string to)
    {
        if (!Directory.Exists(from) || Directory.Exists(to) || File.Exists(to)) return false;
        try
        {
            Directory.Move(from, to);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

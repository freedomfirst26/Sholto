using Sholto.App.Lifecycle.Folders;

namespace Sholto.App.Tests;

/// <summary>A directory rename that behaves as if the two folders were on different drives: it refuses, except
/// for the renames <paramref name="allow"/> lets through (to the real rename) and for a <c>.partial</c> folder
/// being finalised, which in the mover is always a rename within the target drive.</summary>
internal sealed class RefusingDirectoryRename(Func<string, string, bool>? allow = null) : IDirectoryRename
{
    private readonly Func<string, string, bool>? _allow = allow;
    private readonly DirectoryRename _real = new();

    public int Refused { get; private set; }

    public bool TryRename(string from, string to)
    {
        if (from.EndsWith(".partial", StringComparison.Ordinal) || _allow is not null && _allow(from, to)) return _real.TryRename(from, to);
        Refused++;
        return false;
    }
}

namespace Sholto.ExternalTools;

/// <summary>
/// Locates an arm's-length external tool's bare name (madmom, demucs,
/// ffmpeg) on disk. Used ONCE, at bootstrap (<c>App.axaml.cs</c>) — it is not a
/// service to inject and query at runtime. Callers are handed the resolved path (or
/// null) as a plain value; nothing downstream holds a reference to this type.
///
/// Built from <see cref="ExternalToolOptions"/> rather than exposed as static
/// methods: the search directories and the executable suffix are platform facts
/// decided once at bootstrap (<see cref="ExternalToolOptions.ForCurrentPlatform"/>),
/// not constants baked into the type.
///
/// Search order: <c>~/.local/bin</c>, <c>/usr/local/bin</c>, <c>/usr/bin</c>, then
/// <c>PATH</c> (on Linux — see <see cref="ExternalToolOptions"/> for Windows).
/// <c>sholto-deps.sh</c>'s <c>deps_find_binary</c> mirrors this order — the two must
/// not drift.
///
/// Resolution happens once, at the moment <see cref="Locate"/> is called — not lazily
/// on every use. A tool installed while Sholto is already running is no longer picked
/// up without a restart; that trade is intentional (see the 2026-09-11 architectural
/// review notes).
/// </summary>
public sealed class ExternalToolFinder
{
    private readonly ExternalToolOptions _options;
    private readonly IEnumerable<string> _fixedDirs;
    private readonly string? _pathEnv;

    public ExternalToolFinder(ExternalToolOptions options)
        : this(options, options.SearchDirectories, Environment.GetEnvironmentVariable("PATH"))
    {
    }

    /// <summary>Testable seam: the search dirs and the PATH value are injected
    /// instead of read from the real environment.</summary>
    public ExternalToolFinder(ExternalToolOptions options, IEnumerable<string> fixedDirs, string? pathEnv)
    {
        _options = options;
        _fixedDirs = fixedDirs;
        _pathEnv = pathEnv;
    }

    /// <summary>Locate <paramref name="name"/>, or null if it isn't installed
    /// anywhere we look.</summary>
    public string? Locate(string name)
    {
        var fileName = name + _options.ExecutableSuffix;

        foreach (var dir in _fixedDirs)
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) return candidate;
        }

        foreach (var dir in (_pathEnv ?? "").Split(Path.PathSeparator))
        {
            if (dir.Length == 0) continue;
            var full = Path.Combine(dir, fileName);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    /// <summary>Locate <paramref name="name"/>, falling back to the (suffixed) bare
    /// name so the OS walks <c>PATH</c> itself. Use this where a command must be
    /// produced either way — it avoids writing the tool name twice, where a rename
    /// can update one and not the other.</summary>
    public string LocateOrName(string name) => Locate(name) ?? (name + _options.ExecutableSuffix);
}

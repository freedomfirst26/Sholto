namespace Sholto.ExternalTools;

/// <summary>
/// Builds <see cref="ExternalToolOptions"/> — the platform defaults, and the env-var
/// overrides on top of them.
///
/// Split out so the options type is a plain value: a POCO that carries settings and
/// decides nothing. Knowing what a Linux box looks like, what a Windows box looks
/// like, which one we are on, and which environment variables may override it, is a
/// separate job from being the settings — and it is a job that happens exactly once,
/// at the composition root.
/// </summary>
public static class ExternalToolOptionsFactory
{
    /// <summary>Prepended to the search directories when set, so an override binary is
    /// found before every platform default without touching <c>~/.local/bin</c>.</summary>
    public const string ToolPathVariable = "SHOLTO_TOOL_PATH";

    /// <summary>Replaces the cache root entirely when set.</summary>
    public const string CacheRootVariable = "SHOLTO_CACHE_ROOT";

    /// <summary>What the composition root calls: platform defaults with any env-var
    /// overrides applied. Both variables default to today's values, so an unset
    /// environment behaves exactly as before.
    ///
    /// Deliberately NOT extended to <c>DemucsTool.ModelDir</c> or the stem filenames —
    /// those are demucs's own pinned output contract (mirrored by
    /// <c>sholto-deps.sh</c>), not a machine fact. A settable knob there would be a
    /// silent-failure mode, not a convenience.</summary>
    public static ExternalToolOptions FromEnvironment()
    {
        var defaults = ForCurrentPlatform();
        var toolPath = Environment.GetEnvironmentVariable(ToolPathVariable);
        var cacheRoot = Environment.GetEnvironmentVariable(CacheRootVariable);
        if (toolPath is null && cacheRoot is null) return defaults;

        return new ExternalToolOptions
        {
            SearchDirectories = toolPath is null
                ? defaults.SearchDirectories
                : new[] { toolPath }.Concat(defaults.SearchDirectories).ToList(),
            ExecutableSuffix = defaults.ExecutableSuffix,
            CacheRoot = cacheRoot ?? defaults.CacheRoot,
            Tools = defaults.Tools,
        };
    }

    /// <summary>Defaults for the platform this process is actually running on.</summary>
    public static ExternalToolOptions ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? Windows() : Linux();

    /// <summary>Search order: <c>~/.local/bin</c>, <c>/usr/local/bin</c>, <c>/usr/bin</c>,
    /// then <c>PATH</c>. <c>sholto-deps.sh</c>'s <c>deps_find_binary</c> mirrors this
    /// order — the two must not drift.</summary>
    public static ExternalToolOptions Linux() => new()
    {
        SearchDirectories =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"),
            "/usr/local/bin",
            "/usr/bin",
        ],
        ExecutableSuffix = "",
        CacheRoot = DefaultCacheRoot(),
    };

    /// <summary>Windows is not a supported target yet — this exists so the finder and
    /// the analysers are already shaped for it.
    ///
    /// Verified (not guessed) against uv's own storage reference
    /// (https://docs.astral.sh/uv/reference/storage/, cross-checked against
    /// astral-sh/uv issues #7008 and #14693, 2026-09-12): uv's default *executable*
    /// directory — where <c>uv tool install</c> places the shim, as opposed to
    /// <c>UV_TOOL_DIR</c>'s <c>%APPDATA%\uv\data\tools</c>, which holds the tool's
    /// private environment, not something on PATH — is
    /// <c>%XDG_BIN_HOME%</c>, else <c>%XDG_DATA_HOME%\..\bin</c>, else
    /// <c>%USERPROFILE%\.local\bin</c>. XDG vars are essentially never set on
    /// Windows, so in practice that resolves to the same <c>~/.local/bin</c>
    /// convention as Linux — hence one fixed directory here too, built the same way
    /// <see cref="Linux"/> builds it. This governs both of Sholto's uv-installed
    /// tools (demucs, madmom-onnx).
    ///
    /// Deliberately NOT given a <c>%ProgramFiles%</c> entry, unlike the
    /// <c>/usr/local/bin</c>/<c>/usr/bin</c> pair on <see cref="Linux"/>: Windows has
    /// no equivalent convention of dropping a bare executable straight into a
    /// well-known root — a Program Files install of ffmpeg lands in some
    /// installer-chosen subfolder (e.g. <c>...\ffmpeg\bin\</c>), so a fixed
    /// <c>%ProgramFiles%</c> search dir would never actually match anything; it
    /// would be dead code dressed up as coverage. ffmpeg — the one tool here uv
    /// doesn't manage — is left to <c>PATH</c>, same as Linux leaves it to
    /// <c>/usr/bin</c> only by convention, PATH by fallback.</summary>
    public static ExternalToolOptions Windows() => new()
    {
        SearchDirectories =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"),
        ],
        ExecutableSuffix = ".exe",
        CacheRoot = DefaultCacheRoot(),
    };

    private static string DefaultCacheRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sholto");
}

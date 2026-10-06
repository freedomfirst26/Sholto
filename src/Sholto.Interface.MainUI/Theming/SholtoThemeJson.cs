using Avalonia.Platform;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>
/// Loads <see cref="SholtoTheme"/> instances. Themes ship two ways:
///   1. Bundled — JSON files in <c>src/Sholto.Interface.MainUI/Themes/</c>, included via
///      &lt;AvaloniaResource&gt; in the csproj and read at startup via
///      <c>avares://Sholto.Interface.MainUI/Themes/*.json</c>.
///   2. User — drop additional <c>.json</c> files into
///      <c>$XDG_CONFIG_HOME/sholto/themes/</c> (or <c>~/.config/sholto/themes/</c>)
///      and they're merged with the bundled list, no rebuild required.
/// The JSON schema is documented on <see cref="SholtoThemeFactory"/>.
/// </summary>
public sealed class SholtoThemeJson(ISholtoThemeFactory themeFactory) : ISholtoThemeJson
{
    private readonly ISholtoThemeFactory _themeFactory = themeFactory;

    /// <summary>Read all bundled themes (avares://Sholto.Interface.MainUI/Themes/*.json) plus
    /// any user-supplied themes from the config dir. Bundled themes always win
    /// on name collision so a malformed user override can't replace a built-in.
    /// The bundled list ORDER is taken from <c>themes.manifest</c> so we get a
    /// stable, intentional ordering rather than whatever Directory.Enumerate
    /// happens to return.</summary>
    public IReadOnlyList<SholtoTheme> LoadAll()
    {
        var list = new List<SholtoTheme>();
        var bundledNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in LoadBundled())
        {
            list.Add(t);
            bundledNames.Add(t.Name);
        }

        foreach (var t in LoadUserDir())
        {
            if (bundledNames.Contains(t.Name))
            {
                Console.WriteLine($"[Themes] skipping user theme '{t.Name}': name collides with a bundled theme");
                continue;
            }
            list.Add(t);
        }

        return list;
    }

    private IEnumerable<SholtoTheme> LoadBundled()
    {
        // AssetLoader can't enumerate, so we keep a fixed manifest file
        // (themes.manifest) that lists the filenames one per line — drop a new
        // file in src/Sholto.Interface.MainUI/Themes/ + add its name to the manifest and
        // it shows up automatically.
        const string ManifestUri = "avares://Sholto.Interface.MainUI/Themes/themes.manifest";
        var manifestUri = new Uri(ManifestUri);
        if (!AssetLoader.Exists(manifestUri))
        {
            Console.WriteLine($"[Themes] no bundled manifest at {ManifestUri}");
            yield break;
        }

        string[] lines;
        using (var s = AssetLoader.Open(manifestUri))
        using (var sr = new StreamReader(s))
            lines = sr.ReadToEnd()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var name in lines)
        {
            if (name.StartsWith("#")) continue;
            var uri = new Uri($"avares://Sholto.Interface.MainUI/Themes/{name}");
            if (!AssetLoader.Exists(uri))
            {
                Console.WriteLine($"[Themes] manifest references missing file: {name}");
                continue;
            }
            SholtoTheme? theme = null;
            try
            {
                using var s = AssetLoader.Open(uri);
                using var sr = new StreamReader(s);
                theme = _themeFactory.Create(sr.ReadToEnd());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Themes] failed to load bundled '{name}': {ex.Message}");
            }
            if (theme is not null) yield return theme;
        }
    }

    private IEnumerable<SholtoTheme> LoadUserDir()
    {
        var dir = UserThemesDir();
        if (!Directory.Exists(dir)) yield break;
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            SholtoTheme? theme = null;
            try { theme = _themeFactory.Create(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                Console.WriteLine($"[Themes] failed to load user theme '{path}': {ex.Message}");
            }
            if (theme is not null) yield return theme with { IsUser = true };
        }
    }

    /// <summary>Resolved user theme directory. Honours <c>$XDG_CONFIG_HOME</c>
    /// when set so Linux users with non-standard config layouts work without
    /// custom code, otherwise <c>~/.config/sholto/themes/</c>.</summary>
    public string UserThemesDir()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var home = string.IsNullOrEmpty(xdg)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdg;
        return Path.Combine(home, "sholto", "themes");
    }
}

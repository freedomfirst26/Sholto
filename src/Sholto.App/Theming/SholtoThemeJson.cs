using System.Text.Json;
using Avalonia.Media;
using Avalonia.Platform;
using Sholto.App.Controls;

namespace Sholto.App.Theming;

/// <summary>
/// Loads <see cref="SholtoTheme"/> instances from JSON. Themes ship two ways:
///   1. Bundled — JSON files in <c>src/Sholto.App/Themes/</c>, included via
///      &lt;AvaloniaResource&gt; in the csproj and read at startup via
///      <c>avares://Sholto.App/Themes/*.json</c>.
///   2. User — drop additional <c>.json</c> files into
///      <c>$XDG_CONFIG_HOME/opendj/themes/</c> (or <c>~/.config/opendj/themes/</c>)
///      and they're merged with the bundled list, no rebuild required.
///
/// JSON schema (all colour fields are #RRGGBB or #AARRGGBB):
/// <code>
/// {
///   "name": "...",
///   "bgDeep":       "#RRGGBB",
///   "surface":      "#RRGGBB",
///   "surfaceRaised":"#RRGGBB",
///   "border":       "#RRGGBB",
///   "primary":      "#RRGGBB",
///   "accent":       "#RRGGBB",
///   "accentBg":     "#AARRGGBB",
///   "mint":         "#RRGGBB",
///   "textBright":   "#RRGGBB",
///   "textMuted":    "#RRGGBB",
///   "playedFadeColor": "#RRGGBB",
///   "waveformPalette": "Bands"|"Hot"|"Plasma"|...,
///   "camelotPalette": {
///     "hueOffset":       0..360,
///     "saturation":      0..1,
///     "majorLightness":  0..1,
///     "minorLightness":  0..1,
///     "onChipForeground":"#RRGGBB"
///   }
/// }
/// </code>
/// </summary>
public static class SholtoThemeJson
{
    public static SholtoTheme Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        var root = doc.RootElement;

        var cam = root.GetProperty("camelotPalette");
        var camPalette = new CamelotPalette(
            HueOffset:        cam.GetProperty("hueOffset").GetDouble(),
            Saturation:       cam.GetProperty("saturation").GetDouble(),
            MajorLightness:   cam.GetProperty("majorLightness").GetDouble(),
            MinorLightness:   cam.GetProperty("minorLightness").GetDouble(),
            OnChipForeground: Brush(cam, "onChipForeground"));

        return new SholtoTheme(
            Name:            root.GetProperty("name").GetString() ?? "Unnamed",
            BgDeep:          Brush(root, "bgDeep"),
            Surface:         Brush(root, "surface"),
            SurfaceRaised:   Brush(root, "surfaceRaised"),
            Border:          Brush(root, "border"),
            Primary:         Brush(root, "primary"),
            Accent:          Brush(root, "accent"),
            AccentBg:        Brush(root, "accentBg"),
            Mint:            Brush(root, "mint"),
            TextBright:      Brush(root, "textBright"),
            TextMuted:       Brush(root, "textMuted"),
            PlayedFadeColor: ParseColor(root.GetProperty("playedFadeColor").GetString()!),
            WaveformPalette: ParsePalette(root.GetProperty("waveformPalette").GetString()!),
            CamelotPalette:  camPalette);
    }

    /// <summary>Read all bundled themes (avares://Sholto.App/Themes/*.json) plus
    /// any user-supplied themes from the config dir. Bundled themes always win
    /// on name collision so a malformed user override can't replace a built-in.
    /// The bundled list ORDER is taken from <c>themes.manifest</c> so we get a
    /// stable, intentional ordering rather than whatever Directory.Enumerate
    /// happens to return.</summary>
    public static IReadOnlyList<SholtoTheme> LoadAll()
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

    private static IEnumerable<SholtoTheme> LoadBundled()
    {
        // AssetLoader can't enumerate, so we keep a static manifest file
        // (themes.manifest) that lists the filenames one per line — drop a new
        // file in src/Sholto.App/Themes/ + add its name to the manifest and
        // it shows up automatically.
        const string ManifestUri = "avares://Sholto.App/Themes/themes.manifest";
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
            var uri = new Uri($"avares://Sholto.App/Themes/{name}");
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
                theme = Parse(sr.ReadToEnd());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Themes] failed to load bundled '{name}': {ex.Message}");
            }
            if (theme is not null) yield return theme;
        }
    }

    private static IEnumerable<SholtoTheme> LoadUserDir()
    {
        var dir = UserThemesDir();
        if (!Directory.Exists(dir)) yield break;
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            SholtoTheme? theme = null;
            try { theme = Parse(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                Console.WriteLine($"[Themes] failed to load user theme '{path}': {ex.Message}");
            }
            if (theme is not null) yield return theme;
        }
    }

    /// <summary>Resolved user theme directory. Honours <c>$XDG_CONFIG_HOME</c>
    /// when set so Linux users with non-standard config layouts work without
    /// custom code, otherwise <c>~/.config/opendj/themes/</c>.</summary>
    public static string UserThemesDir()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var home = string.IsNullOrEmpty(xdg)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdg;
        return Path.Combine(home, "opendj", "themes");
    }

    private static IBrush Brush(JsonElement el, string name) =>
        new SolidColorBrush(ParseColor(el.GetProperty(name).GetString()!));

    private static Color ParseColor(string hex) => Color.Parse(hex);

    private static WaveformPalette ParsePalette(string name) =>
        Enum.TryParse<WaveformPalette>(name, ignoreCase: true, out var p)
            ? p
            : WaveformPalette.Bands;
}

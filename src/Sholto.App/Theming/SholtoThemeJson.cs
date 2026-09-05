using System.Text.Json;
using Avalonia.Media;
using Avalonia.Platform;

namespace Sholto.App.Theming;

/// <summary>
/// Loads <see cref="SholtoTheme"/> instances from JSON. Themes ship two ways:
///   1. Bundled — JSON files in <c>src/Sholto.App/Themes/</c>, included via
///      &lt;AvaloniaResource&gt; in the csproj and read at startup via
///      <c>avares://Sholto.App/Themes/*.json</c>.
///   2. User — drop additional <c>.json</c> files into
///      <c>$XDG_CONFIG_HOME/sholto/themes/</c> (or <c>~/.config/sholto/themes/</c>)
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
///   },
///   "minimap": {                     // OPTIONAL — see below
///     "backdrop":  "#RRGGBB",
///     "playhead":  "#RRGGBB",
///     "label":     "#RRGGBB",
///     "divider":   "#AARRGGBB",
///     "intro":     "#RRGGBB",
///     "buildUp":   "#RRGGBB",
///     "drop":      "#RRGGBB",
///     "breakdown": "#RRGGBB",
///     "verse":     "#RRGGBB",
///     "chorus":    "#RRGGBB",
///     "bridge":    "#RRGGBB",
///     "outro":     "#RRGGBB"
///   },
///   "waveform": {                    // OPTIONAL — every key optional too
///     "background":  "#RRGGBB",      // baked waveform background
///     "low":         "#RRGGBB",      // bass band   (default: Rekordbox 3-band blue)
///     "mid":         "#RRGGBB",      //             (default: orange)
///     "downbeat":    "#AARRGGBB",    // bar guide   (default: from "waveformPalette" preset)
///     "beatTick":    "#AARRGGBB",    // (default: textBright @C0)
///     "playhead":    "#RRGGBB",      // (default: mint)
///     "marker":      "#RRGGBB",      // (default: accent)
///     "gain":        "#AARRGGBB",    // gain line (default: mint @FF)
///     "loop":        "#AARRGGBB"     // loop band (default: accent @80)
///   }
/// }
/// </code>
/// "high" is not a key — the inner band is always white.
/// The vocal lane, VOX chip, and beat-snap glow are fixed green/grey on every theme and are not themeable.
/// The whole "minimap" section is optional, and so is every key within it — any
/// key that's missing (including the whole section) falls back to a colour
/// derived from the theme's core palette via <see cref="MinimapPalette.DeriveFrom"/>,
/// so every existing theme file keeps working unedited.
/// "waveformPalette" now only seeds the downbeat colour; the three bands default
/// to the Rekordbox scheme for every theme unless "waveform" sets low/mid/high.
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

        var bgDeepColor     = ParseColor(root.GetProperty("bgDeep").GetString()!);
        var primaryColor    = ParseColor(root.GetProperty("primary").GetString()!);
        var accentColor     = ParseColor(root.GetProperty("accent").GetString()!);
        var mintColor       = ParseColor(root.GetProperty("mint").GetString()!);
        var textBrightColor = ParseColor(root.GetProperty("textBright").GetString()!);
        var borderColor     = ParseColor(root.GetProperty("border").GetString()!);
        var minimap = ParseMinimapPalette(root, bgDeepColor, primaryColor, accentColor, mintColor,
            textBrightColor, borderColor);

        var textMutedColor = ParseColor(root.GetProperty("textMuted").GetString()!);
        var preset   = ParsePreset(root.GetProperty("waveformPalette").GetString()!);
        var waveform = ParseWaveformPalette(root, preset, bgDeepColor, accentColor, mintColor,
                                            textBrightColor, textMutedColor);

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
            WaveformPreset:  preset,
            Waveform:        waveform,
            CamelotPalette:  camPalette,
            Minimap:         minimap);
    }

    /// <summary>Parse the optional "minimap" section. The section itself, and every
    /// key within it, is optional — anything missing is derived from the theme's
    /// core colours so themes never need editing to add minimap support.</summary>
    private static MinimapPalette ParseMinimapPalette(JsonElement root, Color bgDeep, Color primary,
        Color accent, Color mint, Color textBright, Color border)
    {
        var derived = MinimapPalette.DeriveFrom(bgDeep, primary, accent, mint, textBright, border);
        if (!root.TryGetProperty("minimap", out var mm)) return derived;

        Color Get(string key, Color fallback) =>
            mm.TryGetProperty(key, out var el) && el.GetString() is string s ? ParseColor(s) : fallback;

        return new MinimapPalette(
            Backdrop:  Get("backdrop",  derived.Backdrop),
            Playhead:  Get("playhead",  derived.Playhead),
            Label:     Get("label",     derived.Label),
            Divider:   Get("divider",   derived.Divider),
            Intro:     Get("intro",     derived.Intro),
            BuildUp:   Get("buildUp",   derived.BuildUp),
            Drop:      Get("drop",      derived.Drop),
            Breakdown: Get("breakdown", derived.Breakdown),
            Verse:     Get("verse",     derived.Verse),
            Chorus:    Get("chorus",    derived.Chorus),
            Bridge:    Get("bridge",    derived.Bridge),
            Outro:     Get("outro",     derived.Outro));
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
    /// custom code, otherwise <c>~/.config/sholto/themes/</c>.</summary>
    public static string UserThemesDir()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var home = string.IsNullOrEmpty(xdg)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdg;
        return Path.Combine(home, "sholto", "themes");
    }

    private static IBrush Brush(JsonElement el, string name) =>
        new SolidColorBrush(ParseColor(el.GetProperty(name).GetString()!));

    private static Color ParseColor(string hex) => Color.Parse(hex);

    private static WaveformPreset ParsePreset(string name) =>
        Enum.TryParse<WaveformPreset>(name, ignoreCase: true, out var p) ? p : WaveformPreset.Bands;

    /// <summary>Parse the optional "waveform" section. The section and every key in
    /// it are optional; anything missing comes from <see cref="WaveformPalette.DeriveFrom"/>.</summary>
    private static WaveformPalette ParseWaveformPalette(JsonElement root, WaveformPreset preset,
        Color bgDeep, Color accent, Color mint, Color textBright, Color textMuted)
    {
        var d = WaveformPalette.DeriveFrom(preset, bgDeep, accent, mint, textBright, textMuted);
        if (!root.TryGetProperty("waveform", out var wf)) return d;

        Color Get(string key, Color fallback) =>
            wf.TryGetProperty(key, out var el) && el.GetString() is string s ? ParseColor(s) : fallback;

        return new WaveformPalette(
            Background:  Get("background",  d.Background),
            Low:         Get("low",         d.Low),
            Mid:         Get("mid",         d.Mid),
            Downbeat:    Get("downbeat",    d.Downbeat),
            BeatTick:    Get("beatTick",    d.BeatTick),
            Playhead:    Get("playhead",    d.Playhead),
            Marker:      Get("marker",      d.Marker),
            Gain:        Get("gain",        d.Gain),
            Loop:        Get("loop",        d.Loop));
    }
}

using System.Text.Json;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>
/// Creates <see cref="SholtoTheme"/> instances from theme JSON (bundled or user-supplied).
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
///     "onChipForeground":"#RRGGBB",  // parsed but no longer drawn anywhere; kept so existing themes still load
///     "keyChipText":     "#RRGGBB"   // OPTIONAL — text on the Camelot key chips (default: defaults.json, white)
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
///   "load": {                        // OPTIONAL — every key optional too (default: defaults.json)
///     "deck1": "#RRGGBB", "deck2": "#RRGGBB", "trackList": "#RRGGBB"
///   },
///   "waveform": {                    // OPTIONAL — every key optional too
///     "background":  "#RRGGBB",      // baked waveform background (default: defaults.json)
///     "low":         "#RRGGBB",      // bass band   (default: Rekordbox 3-band blue)
///     "mid":         "#RRGGBB",      //             (default: orange)
///     "high":        "#RRGGBB",      // inner band  (default: defaults.json)
///     "downbeat":    "#AARRGGBB",    // bar guide   (default: from "waveformPalette" preset)
///     "beatTick":    "#AARRGGBB",    // (default: textBright @C0)
///     "playhead":    "#RRGGBB",      // (default: mint)
///     "marker":      "#RRGGBB",      // (default: accent)
///     "gain":        "#AARRGGBB",    // gain line (default: mint @FF)
///     "loop":        "#AARRGGBB",    // loop band (default: accent @80)
///     "vocal":       "#RRGGBB",      // vocal lane, active   (default: defaults.json)
///     "vocalInactive":"#RRGGBB",     // vocal lane, inactive (default: defaults.json)
///     "snapGlow":    "#RRGGBB",      // beat-snap glow       (default: defaults.json)
///     "gridEdit":    "#RRGGBB",      // tint once the grid was nudged (default: defaults.json)
///     "edge":        "#RRGGBB",      // waveform edge outline (default: defaults.json)
///     "rgbLow":      "#RRGGBB",      // RGB style: bass colour (default: defaults.json)
///     "rgbMid":      "#RRGGBB",      // RGB style: mid colour  (default: defaults.json)
///     "rgbHigh":     "#RRGGBB"       // RGB style: high colour (default: defaults.json)
///   },
///   "stems":     { "drums", "vocals", "instrumental" },          // OPTIONAL, each key optional
///   "status":    { "ok", "warn", "error", "attention" },         // OPTIONAL
///   "mute":      "#RRGGBB",                                      // OPTIONAL
///   "ring":      { "start", "mid", "late", "end" },              // OPTIONAL
///   "tags":      { "chipBg", "chipFg", "indicatorBg", "indicatorFg" }, // OPTIONAL
///   "faceplate": { "rest", "hover", "selected", "glow" },        // OPTIONAL
///   "scrim": "#AARRGGBB", "shadow": "#AARRGGBB", "iconPlate": "#RRGGBB",  // OPTIONAL
///   "knob":      { "arc" }                                       // OPTIONAL: a settings knob's value arc
///                                                                //   (default: defaults.json, green)
/// }
/// </code>
/// Lookup order for every colour key: this theme's JSON, then <c>Themes/defaults.json</c>
/// (same nesting, via <see cref="IThemeDefaults"/>), then derivation from the core colours
/// (<see cref="IWaveformPaletteFactory"/>, <see cref="IMinimapPaletteFactory"/>).
/// The whole "minimap" section is optional, and so is every key within it — any
/// key that's missing (including the whole section) falls back to a colour
/// derived from the theme's core palette via <see cref="IMinimapPaletteFactory"/>,
/// so every existing theme file keeps working unedited.
/// "waveformPalette" only seeds the downbeat colour; the three bands default
/// to the Rekordbox scheme for every theme unless "waveform" sets low/mid/high.
/// </summary>
public sealed class SholtoThemeFactory(IWaveformPaletteFactory waveformPalettes, IMinimapPaletteFactory minimapPalettes, IThemeDefaults defaults) : ISholtoThemeFactory
{
    private readonly IWaveformPaletteFactory _waveformPalettes = waveformPalettes;
    private readonly IMinimapPaletteFactory _minimapPalettes = minimapPalettes;
    private readonly IThemeDefaults _defaults = defaults;

    /// <summary>Creates a theme from its JSON text; throws on malformed or missing required keys.</summary>
    public SholtoTheme Create(string json)
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
            OnChipForeground: Brush(cam, "onChipForeground"),
            KeyChipForeground: new SolidColorBrush(Lookup(root, "camelotPalette", "keyChipText")));

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
            Minimap:         minimap,
            Stems:           new StemPalette(
                Drums:        Lookup(root, "stems", "drums"),
                Vocals:       Lookup(root, "stems", "vocals"),
                Instrumental: Lookup(root, "stems", "instrumental"),
                ChipText:     Lookup(root, "stems", "chipText")),
            Status:          new StatusPalette(
                Ok:        Lookup(root, "status", "ok"),
                Warn:      Lookup(root, "status", "warn"),
                Error:     Lookup(root, "status", "error"),
                Attention: Lookup(root, "status", "attention")),
            Mute:            Lookup(root, "", "mute"),
            Ring:            new RingPalette(
                Start: Lookup(root, "ring", "start"),
                Mid:   Lookup(root, "ring", "mid"),
                Late:  Lookup(root, "ring", "late"),
                End:   Lookup(root, "ring", "end")),
            Tags:            new TagPalette(
                ChipBg:      Lookup(root, "tags", "chipBg"),
                ChipFg:      Lookup(root, "tags", "chipFg"),
                IndicatorBg: Lookup(root, "tags", "indicatorBg"),
                IndicatorFg: Lookup(root, "tags", "indicatorFg")),
            Faceplate:       new FaceplatePalette(
                Rest:     Lookup(root, "faceplate", "rest"),
                Hover:    Lookup(root, "faceplate", "hover"),
                Selected: Lookup(root, "faceplate", "selected"),
                Glow:     Lookup(root, "faceplate", "glow")),
            Scrim:           Lookup(root, "", "scrim"),
            Shadow:          Lookup(root, "", "shadow"),
            IconPlate:       Lookup(root, "", "iconPlate"),
            Knob:            new KnobPalette(
                Arc:     Lookup(root, "knob", "arc"),
                Track:   borderColor,
                Cap:     ParseColor(root.GetProperty("surfaceRaised").GetString()!),
                CapEdge: borderColor,
                Tick:    textMutedColor,
                Pointer: textBrightColor),
            Load:            new LoadPalette(
                Deck1:     Lookup(root, "load", "deck1"),
                Deck2:     Lookup(root, "load", "deck2"),
                TrackList: Lookup(root, "load", "trackList")));
    }

    /// <summary>Theme JSON (<paramref name="section"/> object, or the root when empty) →
    /// defaults.json → <paramref name="fallback"/>.</summary>
    private Color Lookup(JsonElement root, string section, string key, Color? fallback = null)
    {
        var scope = root;
        var found = section.Length == 0 || root.TryGetProperty(section, out scope);
        if (found && scope.ValueKind == JsonValueKind.Object
            && scope.TryGetProperty(key, out var el) && el.GetString() is string s)
            return ParseColor(s);
        var dotted = section.Length == 0 ? key : section + "." + key;
        if (_defaults.TryGetColor(dotted, out var d)) return d;
        // A key missing from both the theme and defaults.json shows as magenta, not a hidden literal.
        return fallback ?? Colors.Magenta;
    }

    /// <summary>Creates the optional "minimap" section. The section itself, and every
    /// key within it, is optional — anything missing is derived from the theme's
    /// core colours so themes never need editing to add minimap support.</summary>
    private MinimapPalette ParseMinimapPalette(JsonElement root, Color bgDeep, Color primary,
        Color accent, Color mint, Color textBright, Color border)
    {
        var derived = _minimapPalettes.FromTheme(bgDeep, primary, accent, mint, textBright, border);

        Color Get(string key, Color fallback) => Lookup(root, "minimap", key, fallback);

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

    private IBrush Brush(JsonElement el, string name) =>
        new SolidColorBrush(ParseColor(el.GetProperty(name).GetString()!));

    private Color ParseColor(string hex) => Color.Parse(hex);

    private WaveformPreset ParsePreset(string name) =>
        Enum.TryParse<WaveformPreset>(name, ignoreCase: true, out var p) ? p : WaveformPreset.Bands;

    /// <summary>Creates the optional "waveform" section. The section and every key in
    /// it are optional; anything missing comes from <see cref="IWaveformPaletteFactory"/>.</summary>
    private WaveformPalette ParseWaveformPalette(JsonElement root, WaveformPreset preset,
        Color bgDeep, Color accent, Color mint, Color textBright, Color textMuted)
    {
        var d = _waveformPalettes.FromTheme(preset, bgDeep, accent, mint, textBright, textMuted);

        Color Get(string key, Color fallback) => Lookup(root, "waveform", key, fallback);

        return new WaveformPalette(
            Background:  Get("background",  d.Background),
            Low:         Get("low",         d.Low),
            Mid:         Get("mid",         d.Mid),
            Downbeat:    Get("downbeat",    d.Downbeat),
            BeatTick:    Get("beatTick",    d.BeatTick),
            Playhead:    Get("playhead",    d.Playhead),
            Marker:      Get("marker",      d.Marker),
            Gain:        Get("gain",        d.Gain),
            Loop:        Get("loop",        d.Loop),
            High:          Get("high",          d.High),
            Vocal:         Get("vocal",         d.Vocal),
            VocalInactive: Get("vocalInactive", d.VocalInactive),
            SnapGlow:      Get("snapGlow",      d.SnapGlow),
            GridEdit:      Get("gridEdit",      d.GridEdit),
            Edge:          Get("edge",          d.Edge),
            RgbLow:        Get("rgbLow",        d.RgbLow),
            RgbMid:        Get("rgbMid",        d.RgbMid),
            RgbHigh:       Get("rgbHigh",       d.RgbHigh));
    }
}

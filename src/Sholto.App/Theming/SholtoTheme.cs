using Avalonia;
using Avalonia.Media;
using Sholto.App.Controls;

namespace Sholto.App.Theming;

/// <summary>
/// All UI surface colors. Bind XAML brushes/colors to properties on this so
/// switching <see cref="Themes"/> retones the whole app live.
/// </summary>
public sealed record SholtoTheme(
    string Name,
    IBrush BgDeep,           // window background
    IBrush Surface,          // track list background
    IBrush SurfaceRaised,    // menu bar, deck panel
    IBrush Border,           // panel dividers, vinyl ring
    IBrush Primary,          // DECK A label, primary action
    IBrush Accent,           // BPM number, downbeat highlights
    IBrush AccentBg,         // BPM badge background (semi-transparent of Accent)
    IBrush Mint,             // playhead, needle, "alive" states
    IBrush TextBright,       // primary text
    IBrush TextMuted,        // secondary text
    Color  PlayedFadeColor,  // background color used by the played-half gradient
    WaveformPalette WaveformPalette
)
{
    /// <summary>Album-art radial gradient using the theme's primary + accent.</summary>
    public IBrush AlbumArtBrush
    {
        get
        {
            var brush = new RadialGradientBrush
            {
                Center = new RelativePoint(0.35, 0.35, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.35, 0.35, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.65, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.65, RelativeUnit.Relative),
            };
            var accentColor = (Accent as SolidColorBrush)?.Color ?? Colors.Magenta;
            var primaryColor = (Primary as SolidColorBrush)?.Color ?? Colors.BlueViolet;
            brush.GradientStops.Add(new GradientStop(accentColor, 0.0));
            brush.GradientStops.Add(new GradientStop(primaryColor, 1.0));
            return brush;
        }
    }

    /// <summary>Horizontal gradient fading from PlayedFadeColor on the left to transparent.</summary>
    public IBrush PlayedFadeGradient
    {
        get
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint   = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            };
            brush.GradientStops.Add(new GradientStop(PlayedFadeColor, 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xCC, PlayedFadeColor.R, PlayedFadeColor.G, PlayedFadeColor.B), 0.6));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, PlayedFadeColor.R, PlayedFadeColor.G, PlayedFadeColor.B), 1.0));
            return brush;
        }
    }
}

public static class Themes
{
    private static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));
    private static Color  C(string hex) => Color.Parse(hex);

    public static SholtoTheme Classic { get; } = new(
        Name: "Classic",
        BgDeep:        B("#111111"),
        Surface:       B("#1A1A1A"),
        SurfaceRaised: B("#222222"),
        Border:        B("#333333"),
        Primary:       B("#00FFCC"),
        Accent:        B("#FFC700"),
        AccentBg:      B("#33FFC700"),
        Mint:          B("#FFFFFF"),
        TextBright:    B("#EEEEEE"),
        TextMuted:     B("#888888"),
        PlayedFadeColor: C("#111111"),
        WaveformPalette: WaveformPalette.Bands
    );


    public static SholtoTheme Serato { get; } = new(
        Name: "Serato",
        BgDeep:        B("#0F0F0F"),
        Surface:       B("#161616"),
        SurfaceRaised: B("#1E1E1E"),
        Border:        B("#3D3D3D"),
        Primary:       B("#FF3D3D"),
        Accent:        B("#3D8BFF"),
        AccentBg:      B("#333D8BFF"),
        Mint:          B("#3DFF7A"),
        TextBright:    B("#F2F2F2"),
        TextMuted:     B("#909090"),
        PlayedFadeColor: C("#0F0F0F"),
        WaveformPalette: WaveformPalette.Hot
    );

    /// <summary>Smoke — late-night vinyl-bar moody. Warm charcoal browns with
    /// whiskey amber and candle gold. No neon; this one feels lit by candles.</summary>
    public static SholtoTheme Smoke { get; } = new(
        Name: "Smoke",
        BgDeep:        B("#1C1917"),
        Surface:       B("#2A2522"),
        SurfaceRaised: B("#332C28"),
        Border:        B("#463C36"),
        Primary:       B("#A88468"),
        Accent:        B("#F2C879"),
        AccentBg:      B("#33F2C879"),
        Mint:          B("#D4A574"),
        TextBright:    B("#F0E5D2"),
        TextMuted:     B("#8A7B6E"),
        PlayedFadeColor: C("#1C1917"),
        WaveformPalette: WaveformPalette.Smoke
    );

    /// <summary>Tokyo Night — cyberpunk-rain neon. Deep navy with hot magenta
    /// and electric cyan. Crowd-pleaser dev-theme aesthetic.</summary>
    public static SholtoTheme TokyoNight { get; } = new(
        Name: "Tokyo Night",
        BgDeep:        B("#0F172A"),
        Surface:       B("#1A2238"),
        SurfaceRaised: B("#222C46"),
        Border:        B("#2F3B57"),
        Primary:       B("#7AA2F7"),
        Accent:        B("#FF7AC6"),
        AccentBg:      B("#33FF7AC6"),
        Mint:          B("#7AE6FF"),
        TextBright:    B("#E2E7F5"),
        TextMuted:     B("#7C89B8"),
        PlayedFadeColor: C("#0F172A"),
        WaveformPalette: WaveformPalette.Plasma
    );

    /// <summary>Catppuccin Mocha — soft pastel peach / mauve / sky on warm dark.
    /// The cosy dev-community favourite; easy on the eyes for long sessions.</summary>
    public static SholtoTheme CatppuccinMocha { get; } = new(
        Name: "Catppuccin Mocha",
        BgDeep:        B("#1E1E2E"),
        Surface:       B("#28283C"),
        SurfaceRaised: B("#313244"),
        Border:        B("#45475A"),
        Primary:       B("#CBA6F7"),
        Accent:        B("#F5C2E7"),
        AccentBg:      B("#33F5C2E7"),
        Mint:          B("#94E2D5"),
        TextBright:    B("#CDD6F4"),
        TextMuted:     B("#7F849C"),
        PlayedFadeColor: C("#1E1E2E"),
        WaveformPalette: WaveformPalette.Plasma
    );

    /// <summary>Glacier — calm Nordic dark. Slate-blue surfaces with frost and
    /// aurora-violet accents. Reads more "studio" than "club".</summary>
    public static SholtoTheme Glacier { get; } = new(
        Name: "Glacier",
        BgDeep:        B("#1E293B"),
        Surface:       B("#243044"),
        SurfaceRaised: B("#293548"),
        Border:        B("#3B4860"),
        Primary:       B("#88C0D0"),
        Accent:        B("#B48EAD"),
        AccentBg:      B("#33B48EAD"),
        Mint:          B("#A3BE8C"),
        TextBright:    B("#ECEFF4"),
        TextMuted:     B("#7B8A9E"),
        PlayedFadeColor: C("#1E293B"),
        WaveformPalette: WaveformPalette.Glacier
    );

    /// <summary>Bloodmoon — high-drama. Near-black carbon with crimson and bone.
    /// Aggressive / metal / dark-techno energy.</summary>
    public static SholtoTheme Bloodmoon { get; } = new(
        Name: "Bloodmoon",
        BgDeep:        B("#0A0606"),
        Surface:       B("#140B0B"),
        SurfaceRaised: B("#1A0E0E"),
        Border:        B("#2E1414"),
        Primary:       B("#C8324D"),
        Accent:        B("#E8A54B"),
        AccentBg:      B("#33C8324D"),
        Mint:          B("#F5E6D8"),
        TextBright:    B("#F2E8DC"),
        TextMuted:     B("#8A6F65"),
        PlayedFadeColor: C("#0A0606"),
        WaveformPalette: WaveformPalette.Bloodmoon
    );

    public static IReadOnlyList<SholtoTheme> All { get; } =
        [Classic, Serato, Smoke, TokyoNight, CatppuccinMocha, Glacier, Bloodmoon];
}

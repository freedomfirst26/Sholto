using Avalonia;
using Avalonia.Media;
using OpenDJ.App.Controls;

namespace OpenDJ.App.Theming;

/// <summary>
/// All UI surface colors. Bind XAML brushes/colors to properties on this so
/// switching <see cref="Themes"/> retones the whole app live.
/// </summary>
public sealed record OpenDjTheme(
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

    public static OpenDjTheme Classic { get; } = new(
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

    public static OpenDjTheme Plasma { get; } = new(
        Name: "Plasma",
        BgDeep:        B("#0B0918"),
        Surface:       B("#16122B"),
        SurfaceRaised: B("#1F1A3D"),
        Border:        B("#2D2752"),
        Primary:       B("#7C5CFF"),
        Accent:        B("#FF4E9A"),
        AccentBg:      B("#33FF4E9A"),
        Mint:          B("#34F0C6"),
        TextBright:    B("#EEEAFF"),
        TextMuted:     B("#8B7FB8"),
        PlayedFadeColor: C("#0B0918"),
        WaveformPalette: WaveformPalette.Plasma
    );

    public static OpenDjTheme Serato { get; } = new(
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

    public static IReadOnlyList<OpenDjTheme> All { get; } = [Classic, Plasma, Serato];
}

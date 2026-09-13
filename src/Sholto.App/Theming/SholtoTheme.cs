using Avalonia;
using Avalonia.Media;

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
    WaveformPreset  WaveformPreset,   // named seed from "waveformPalette" (downbeat colour by default)
    WaveformPalette Waveform,         // every colour the deck waveform draws — see WaveformPalette
    CamelotPalette CamelotPalette,
    MinimapPalette Minimap
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

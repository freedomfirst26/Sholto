using Avalonia;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>
/// All UI surface colors. Bind XAML brushes/colors to properties on this so
/// switching <see cref="IThemeCatalog"/> retones the whole app live.
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
    MinimapPalette Minimap,
    StemPalette Stems,
    StatusPalette Status,
    Color Mute,              // muted-deck tint
    RingPalette Ring,
    TagPalette Tags,
    FaceplatePalette Faceplate,
    Color Scrim,             // overlay backdrop (alpha included)
    Color Shadow,            // deck shadow / vignette (alpha included)
    Color IconPlate,         // window icon background plate
    KnobPalette Knob,        // Settings knobs (value arc from "knob.arc", the rest from the core colours)
    LoadPalette Load         // load destinations: deck 1, deck 2, Track List
)
{
    /// <summary>True for a theme loaded from the user themes folder (not one of the bundled themes).</summary>
    public bool IsUser { get; init; }

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

using Avalonia.Media;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Segments;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>
/// Colours for the whole-song section minimap strip (<see cref="Sholto.Interface.MainUI.Controls.MinimapControl"/>).
/// Every bundled/user theme gets one — either an explicit "minimap" JSON section
/// (<see cref="SholtoThemeJson"/>) or, when that section (or a given key) is
/// absent, a palette <see cref="MinimapPaletteFactory"/> computes from the theme's existing
/// colours so no theme file needs editing to stay valid.
/// </summary>
public sealed record MinimapPalette(
    Color Backdrop,
    Color Playhead,
    Color Label,
    Color Divider,
    Color Intro,
    Color BuildUp,
    Color Drop,
    Color Breakdown,
    Color Verse,
    Color Chorus,
    Color Bridge,
    Color Outro)
{
    public Color For(SegmentKind kind) => kind switch
    {
        SegmentKind.Intro     => Intro,
        SegmentKind.BuildUp   => BuildUp,
        SegmentKind.Drop      => Drop,
        SegmentKind.Breakdown => Breakdown,
        SegmentKind.Verse     => Verse,
        SegmentKind.Chorus    => Chorus,
        SegmentKind.Bridge    => Bridge,
        SegmentKind.Outro     => Outro,
        _                     => Intro,
    };
}

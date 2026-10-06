using Avalonia.Media;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>
/// Colours for the whole-song section minimap strip (<see cref="Sholto.Interface.MainUI.Controls.MinimapControl"/>).
/// Every bundled/user theme gets one — either an explicit "minimap" JSON section
/// (<see cref="SholtoThemeFactory"/>) or, when that section (or a given key) is
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
    /// <summary>The colour of a section kind; a neutral <see cref="DeckSectionKind.Section"/> takes the label
    /// colour, which the minimap bakes as a quiet tint.</summary>
    public Color For(DeckSectionKind kind) => kind switch
    {
        DeckSectionKind.Intro     => Intro,
        DeckSectionKind.Build     => BuildUp,
        DeckSectionKind.Drop      => Drop,
        DeckSectionKind.Breakdown => Breakdown,
        DeckSectionKind.Verse     => Verse,
        DeckSectionKind.Chorus    => Chorus,
        DeckSectionKind.Bridge    => Bridge,
        DeckSectionKind.Outro     => Outro,
        _                         => Label,
    };
}

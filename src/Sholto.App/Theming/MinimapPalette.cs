using Avalonia.Media;
using Sholto.Analysis;

namespace Sholto.App.Theming;

/// <summary>
/// Colours for the whole-song section minimap strip (<see cref="Sholto.App.Controls.MinimapControl"/>).
/// Every bundled/user theme gets one — either an explicit "minimap" JSON section
/// (<see cref="SholtoThemeJson"/>) or, when that section (or a given key) is
/// absent, a palette <see cref="DeriveFrom"/> computes from the theme's existing
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

    /// <summary>Blend <paramref name="a"/> toward <paramref name="b"/> by
    /// <paramref name="t"/> (0 = a, 1 = b), channel-wise, alpha held at 0xFF.</summary>
    private static Color Blend(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    /// <summary>Compute a full palette from a theme's already-parsed core colours,
    /// used whenever a theme's JSON omits the "minimap" section entirely, or
    /// omits individual keys within it.</summary>
    public static MinimapPalette DeriveFrom(Color bgDeep, Color primary, Color accent, Color mint,
        Color textBright, Color border)
    {
        var backdrop  = bgDeep;
        var playhead  = mint;
        var label     = textBright;
        var divider   = WithAlpha(bgDeep, 0xB0);
        var drop      = accent;
        var chorus    = Blend(accent, mint, 0.30);
        var buildUp   = primary;
        var verse     = Blend(primary, border, 0.50);
        var breakdown = Blend(accent, primary, 0.50);
        var bridge    = mint;
        var intro     = border;
        var outro     = Blend(border, bgDeep, 0.50);

        return new MinimapPalette(backdrop, playhead, label, divider,
            intro, buildUp, drop, breakdown, verse, chorus, bridge, outro);
    }
}

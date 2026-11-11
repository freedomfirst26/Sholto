using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>Repaints the backdrop scrim and the feather from the current theme.
/// <para>The scrim used to be a flat <c>#C4000000</c>, which is 77% pure black and
/// crushed every theme to the same near-zero luminance: Front Line Assembly's warm
/// brown ground and The Birthday Massacre's purple both arrived as black, and the guide
/// was theme-blind by construction. It is now the theme's own <c>bgDeep</c>
/// darkened, so the field keeps the app's hue while still going darker than any
/// surface in it — the app behind stays legible as context and stops competing with
/// the drawing for attention.</para>
/// <para>The edge where the panel meets the board is three layers. The feather
/// darkens the board on its approach; the panel's cast shadow gives it depth; and
/// the panel's own leading edge is dipped into that shade before it comes up to
/// full surface colour. The third one is the one that does the work: every Sholto
/// theme is dark, so the field beside the panel is already close to black and a
/// darker feather has almost nowhere left to go — but the panel is the LIGHTER of
/// the two, so the step is a step UP, and the only place to soften it is on the
/// panel's own side. The 1px border still terminates it as a crisp line.</para>
/// </summary>
public sealed class FaceplateChrome : IFaceplateChrome
{
    public void Paint(Visual host, Panel backdrop, Border feather, Border panel)
    {
        var deep = host.ResourceColor("SholtoBgDeep", Colors.Transparent);
        Color Shade(byte alpha, double factor) => Color.FromArgb(
            alpha, (byte)(deep.R * factor), (byte)(deep.G * factor), (byte)(deep.B * factor));

        backdrop.Background = new SolidColorBrush(Shade(0xF6, 0.55));

        var featherBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        };
        featherBrush.GradientStops.Add(new GradientStop(Shade(0x00, 0.0), 0.0));
        featherBrush.GradientStops.Add(new GradientStop(Shade(0x70, 0.0), 0.55));
        featherBrush.GradientStops.Add(new GradientStop(Shade(0xC8, 0.0), 1.0));
        feather.Background = featherBrush;

        var raised = host.ResourceColor("SholtoSurfaceRaised", Colors.Transparent);
        Color Mix(Color a, Color b, double t) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

        var face = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        };
        face.GradientStops.Add(new GradientStop(Mix(raised, deep, 0.82), 0.0));
        face.GradientStops.Add(new GradientStop(raised, 0.10));
        face.GradientStops.Add(new GradientStop(raised, 1.0));
        panel.Background = face;

        // The panel's cast shadow: the theme's shadow colour at the alpha the panel always
        // had. BoxShadow takes a colour, not a brush, so it cannot be a DynamicResource.
        var shadow = host.ResourceColor("SholtoShadow", Colors.Transparent);
        panel.BoxShadow = new BoxShadows(new BoxShadow
        {
            OffsetX = -24, Blur = 64, Color = Color.FromArgb(0xB3, shadow.R, shadow.G, shadow.B),
        });
    }
}

using Avalonia.Media;

namespace Sholto.Faceplate.Views;

/// <summary>The Faceplate's fixed colours. Everything else in the overlay comes through
/// <c>{DynamicResource Sholto…}</c> and changes with the theme. These do not: the
/// drawing is a picture of a physical object, and the colour language has to mean the
/// same thing in every theme.
/// <para><b>Amber marks what Sholto uses.</b> A control the app acts on is tinted amber
/// at rest; a control it never hears from carries no colour at all. That way the board
/// answers "what does this thing actually do?" before anyone clicks or reads a
/// legend — which is why there is no legend.</para></summary>
public static class FaceplateBrushes
{
    /// <summary>Resting tint on a control Sholto acts on. Faint enough not to compete
    /// with hover or selection, strong enough that a glance at the board separates the
    /// live controls from the dead ones without looking for them.</summary>
    public static readonly IBrush AmberRest =
        new SolidColorBrush(Color.FromArgb(0x42, 0xFF, 0xCE, 0x7A));

    /// <summary>Resting tint under a pointer. The same wash, lifted, so the control the
    /// pointer is over separates from its neighbours before the bloom is read.</summary>
    public static readonly IBrush AmberHoverFill =
        new SolidColorBrush(Color.FromArgb(0x7A, 0xFF, 0xD2, 0x84));

    /// <summary>Held selection. Unmistakably the strongest of the three, because it has
    /// to say "this is the one the panel is describing" from across the board.</summary>
    public static readonly IBrush AmberSelectedFill =
        new SolidColorBrush(Color.FromArgb(0x9E, 0xFF, 0xDC, 0x96));

    /// <summary>Hover bloom, click blink and sustained selection. One colour doing three
    /// jobs at three intensities, so they read as the same language.</summary>
    public static readonly Color AmberGlow = Color.FromRgb(0xFF, 0xB0, 0x40);

    /// <summary>The bloom, as a brush. Painted as a thick blurred ring hugging the
    /// control rather than an outline, so the light spills outward and the control
    /// underneath stays readable.</summary>
    public static readonly IBrush AmberGlowBrush = new SolidColorBrush(AmberGlow);
}

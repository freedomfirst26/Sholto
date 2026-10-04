using Avalonia;
using Avalonia.Media;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>The Faceplate's amber language, taken from the theme's <c>faceplate</c> colours
/// (<c>SholtoFaceplateRest/Hover/Selected/Glow</c>) and resolved at each read, so a theme
/// change reaches the next paint. Only the alpha bytes live here. Until the overlay
/// calls <see cref="Attach"/> every colour is transparent.
/// <para><b>Amber marks what Sholto uses.</b> A control the app acts on is tinted amber
/// at rest; a control it never hears from carries no colour at all. That way the board
/// answers "what does this thing actually do?" before anyone clicks or reads a
/// legend — which is why there is no legend.</para></summary>
public sealed class FaceplateBrushes
{
    private Visual? _host;

    /// <summary>Binds these brushes to the control whose window carries the
    /// <c>Sholto…</c> resources. Called by the overlay as it is built.</summary>
    public void Attach(Visual host) => _host = host;

    private Color Resolve(string key, byte? alpha)
    {
        var c = _host?.ResourceColor(key, Colors.Transparent) ?? Colors.Transparent;
        return alpha is { } a ? Color.FromArgb(a, c.R, c.G, c.B) : c;
    }

    /// <summary>Resting tint on a control Sholto acts on. Faint enough not to compete
    /// with hover or selection, strong enough that a glance at the board separates the
    /// live controls from the dead ones without looking for them.</summary>
    public IBrush AmberRest => new SolidColorBrush(Resolve("SholtoFaceplateRest", 0x42));

    /// <summary>Resting tint under a pointer. The same wash, lifted, so the control the
    /// pointer is over separates from its neighbours before the bloom is read.</summary>
    public IBrush AmberHoverFill => new SolidColorBrush(Resolve("SholtoFaceplateHover", 0x7A));

    /// <summary>Held selection. Unmistakably the strongest of the three, because it has
    /// to say "this is the one the panel is describing" from across the board.</summary>
    public IBrush AmberSelectedFill => new SolidColorBrush(Resolve("SholtoFaceplateSelected", 0x9E));

    /// <summary>Hover bloom, click blink and sustained selection. One colour doing three
    /// jobs at three intensities, so they read as the same language.</summary>
    public Color AmberGlow => Resolve("SholtoFaceplateGlow", null);

    /// <summary>The bloom, as a brush. Painted as a thick blurred ring hugging the
    /// control rather than an outline, so the light spills outward and the control
    /// underneath stays readable.</summary>
    public IBrush AmberGlowBrush => new SolidColorBrush(AmberGlow);
}

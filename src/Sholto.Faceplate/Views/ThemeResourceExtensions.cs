using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Sholto.Faceplate.Views;

/// <summary>Shared "ask the window directly" workaround for resolving a Sholto theme
/// resource from a control that is not itself the root of the resource lookup.
/// Used by both <see cref="FaceplateOverlay"/> and <see cref="ControlChip"/>, which
/// each need to resolve the same <c>Sholto…</c> brushes painted outside any XAML
/// binding.</summary>
public static class ThemeResourceExtensions
{
    /// <summary>Resolves one of the host's <c>Sholto…</c> theme brushes.
    /// <para>The second lookup is not belt-and-braces, it is the one that works. A
    /// resource lookup started at this control returns nothing for these keys even
    /// once the overlay is attached and the very same keys resolve from the window —
    /// verified by printing both at runtime, attached, with the app's theme live:
    /// <c>this.TryGetResource</c> false, <c>TopLevel.TryGetResource</c> true. Sholto
    /// declares its palette in <c>Window.Resources</c> and rewrites it there on every
    /// theme change, so the window is where these keys live; ask it directly. Without
    /// this every colour in the panel silently fell back to the hard-coded literal
    /// passed in — grey labels and an ORANGE eyebrow in an eleven-theme app.</para>
    /// </summary>
    public static IBrush ResourceBrush(this Visual visual, string key, IBrush fallback)
    {
        if (visual.TryGetResource(key, visual.ActualThemeVariant, out var v) && v is IBrush b) return b;
        var top = TopLevel.GetTopLevel(visual);
        if (top is not null && top.TryGetResource(key, top.ActualThemeVariant, out var w) && w is IBrush c)
            return c;
        return fallback;
    }

    /// <summary>The theme colours are exposed as <c>IBrush</c>, not <c>Color</c>, so a
    /// <c>GradientStop Color="{DynamicResource …}"</c> cannot resolve one. Anything
    /// theme-derived that needs a raw colour — the scrim, the feather — unwraps it
    /// here instead of being written in XAML.</summary>
    public static Color ResourceColor(this Visual visual, string key, Color fallback) =>
        visual.ResourceBrush(key, new SolidColorBrush(fallback)) is SolidColorBrush b ? b.Color : fallback;
}

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sholto.App.Converters;

/// <summary>
/// Picks the loop-band overlay colour for the waveform: theme accent
/// while the loop is on madmom's default grid, red when the user has
/// tapped at least one nudge (so it's visible at a glance that the loop
/// is on a tuned grid). Wired as a MultiBinding in DeckView.axaml taking
/// (IsGridNudged, Theme.LoopBandColor).
/// </summary>
public sealed class LoopColorConverter : IMultiValueConverter
{
    public static readonly LoopColorConverter Instance = new();

    // ~63 % alpha red — same opacity as Theme.LoopBandColor so the
    // transition from theme accent → red doesn't change the band's
    // visual weight, only its hue.
    private static readonly Color NudgedColor = Color.FromArgb(0xA0, 0xE5, 0x39, 0x35);

    public object Convert(System.Collections.Generic.IList<object?> values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        bool nudged = values.Count > 0 && values[0] is bool b && b;
        if (nudged) return NudgedColor;
        if (values.Count > 1 && values[1] is Color c) return c;
        return Color.FromArgb(0x80, 0xFF, 0xC7, 0x00);
    }
}

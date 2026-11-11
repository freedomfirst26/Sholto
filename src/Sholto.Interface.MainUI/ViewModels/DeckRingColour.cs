using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

public sealed class DeckRingColour : IDeckRingColour
{
    private readonly IThemeContext _theme;
    // Cached so we don't allocate a fresh brush every 16 ms: that was Avalonia's binding system seeing a
    // "new" IBrush per frame and invalidating the whole disc.
    private readonly Avalonia.Media.SolidColorBrush _brush = new();
    private double _lastNotifyPos = -1;

    public DeckRingColour(IThemeContext theme)
    {
        _theme = theme;
        Recompute(0);
    }

    public Avalonia.Media.IBrush Brush => _brush;

    public bool MoveTo(double playPosition)
    {
        // Notify only when the visible colour actually changes. Avoids ~120 brush invalidations/sec across
        // both decks.
        Recompute(playPosition);
        if (Math.Abs(playPosition - _lastNotifyPos) <= 0.01) return false;
        _lastNotifyPos = playPosition;
        return true;
    }

    public void Recompute(double playPosition)
    {
        var ring = _theme.Current.Ring;
        (byte r, byte g, byte b) green  = (ring.Start.R, ring.Start.G, ring.Start.B);
        (byte r, byte g, byte b) yellow = (ring.Mid.R, ring.Mid.G, ring.Mid.B);
        (byte r, byte g, byte b) orange = (ring.Late.R, ring.Late.G, ring.Late.B);
        (byte r, byte g, byte b) red    = (ring.End.R, ring.End.G, ring.End.B);
        double p = Math.Clamp(playPosition, 0, 1);

        (byte r, byte g, byte b) Lerp((byte r, byte g, byte b) a, (byte r, byte g, byte b) b, double t) =>
            ((byte)(a.r + (b.r - a.r) * t),
             (byte)(a.g + (b.g - a.g) * t),
             (byte)(a.b + (b.b - a.b) * t));

        var c = p switch
        {
            < 0.5  => Lerp(green,  yellow, p / 0.5),
            < 0.75 => Lerp(yellow, orange, (p - 0.5) / 0.25),
            _      => Lerp(orange, red,    (p - 0.75) / 0.25),
        };
        // Assign only on a real change: Avalonia's Color setter allocates, and the colour moves only near
        // the end of a track, on load, or on theme change.
        var next = Avalonia.Media.Color.FromRgb(c.r, c.g, c.b);
        if (_brush.Color != next)
            _brush.Color = next;
    }
}

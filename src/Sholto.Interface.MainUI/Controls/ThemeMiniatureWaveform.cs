using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>The little static waveform on a Layout Wizard theme card, drawn in that theme's waveform colours
/// as nested low/mid/high 3-BAND layers whatever style is chosen, so the cards show how the themes differ.
/// A fixed snapshot of an invented groove, not a bake of the demo track, so a card costs a few rectangles to
/// paint and nothing per frame. Draws nothing until its palette arrives.</summary>
public sealed class ThemeMiniatureWaveform : Control
{
    private const int Columns = 44;
    private const double Gap = 1;

    public static readonly StyledProperty<WaveformPalette?> PaletteProperty =
        AvaloniaProperty.Register<ThemeMiniatureWaveform, WaveformPalette?>(nameof(Palette));

    // Column shapes of the invented groove: loudness 0..1, and how much mid / treble each carries.
    private readonly double[] _level = new double[Columns];
    private readonly double[] _mid = new double[Columns];
    private readonly double[] _treble = new double[Columns];

    public ThemeMiniatureWaveform()
    {
        for (int i = 0; i < Columns; i++)
        {
            int beat = i % 11;
            bool kick = beat < 2;
            bool hat = beat == 5 || beat == 6;
            double swell = 0.55 + 0.45 * Math.Sin(i * 0.23);
            _mid[i] = hat ? 0.35 : 0.55 + 0.2 * Math.Cos(i * 0.5);
            _treble[i] = hat ? 1.0 : 0.25 + 0.2 * Math.Sin(i * 1.7 + 1);
            _level[i] = Math.Clamp((kick ? 1.0 : 0.62) * swell + (hat ? 0.2 : 0), 0.12, 1.0);
        }
    }

    public WaveformPalette? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PaletteProperty) InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Palette is not { } p || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        double w = Bounds.Width, h = Bounds.Height, mid = h / 2;
        context.FillRectangle(new SolidColorBrush(p.Background), new Rect(0, 0, w, h));
        double pitch = w / Columns;
        double barW = Math.Max(1, pitch - Gap);
        for (int i = 0; i < Columns; i++)
        {
            double x = i * pitch;
            double half = _level[i] * (mid - 1);
            Layer(context, p.Low, x, barW, mid, half);
            Layer(context, p.Mid, x, barW, mid, half * 0.62 * Math.Max(_mid[i], 0.4));
            Layer(context, p.High, x, barW, mid, half * 0.32 * Math.Max(_treble[i], 0.5));
        }
    }

    private void Layer(DrawingContext context, Color colour, double x, double width, double mid, double half) =>
        context.FillRectangle(new SolidColorBrush(colour), new Rect(x, mid - half, width, half * 2));
}

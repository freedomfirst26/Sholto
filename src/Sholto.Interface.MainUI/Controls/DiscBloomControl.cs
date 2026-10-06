using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>
/// The live fill of a deck disc's centre circle: a dark base with three soft glows in the theme's
/// bass / mid / high waveform colours (large bottom-left, medium right, small top), plus a faint dark
/// vignette in the middle so the BPM pill keeps its contrast. Each glow's opacity is one value from
/// <see cref="Levels"/>.
///
/// Plain <see cref="DrawingContext"/> primitives. The four gradient brushes are built once; a level step
/// only sets a brush Opacity, a palette or base change only sets stop colours in place. A control created
/// from XAML draws nothing until its palette and base brush arrive.
/// </summary>
public sealed class DiscBloomControl : Control
{
    public static readonly StyledProperty<WaveformPalette?> PaletteProperty =
        AvaloniaProperty.Register<DiscBloomControl, WaveformPalette?>(nameof(Palette));

    /// <summary>The dark base under the glows (the theme's BgDeep).</summary>
    public static readonly StyledProperty<IBrush?> BaseProperty =
        AvaloniaProperty.Register<DiscBloomControl, IBrush?>(nameof(Base));

    public static readonly StyledProperty<IDiscBloomLevels?> LevelsProperty =
        AvaloniaProperty.Register<DiscBloomControl, IDiscBloomLevels?>(nameof(Levels));

    // Glow geometry is relative to the control (0.5, 0.5 = centre; radius 0.5 = the control's half width),
    // i.e. the design's offsets in units of the circle's radius r: low (-0.45r, +0.85r) radius 1.3r,
    // mid (+0.75r, +0.5r) radius 1.0r, high (+0.2r, -0.85r) radius 0.7r.
    private readonly RadialGradientBrush _low;
    private readonly RadialGradientBrush _mid;
    private readonly RadialGradientBrush _high;
    private readonly RadialGradientBrush _vignette;
    private readonly Action _onLevelsChanged;

    static DiscBloomControl()
    {
        AffectsRender<DiscBloomControl>(PaletteProperty, BaseProperty);
        PaletteProperty.Changed.AddClassHandler<DiscBloomControl>((c, _) => c.Retone());
        BaseProperty.Changed.AddClassHandler<DiscBloomControl>((c, _) => c.Retone());
        LevelsProperty.Changed.AddClassHandler<DiscBloomControl>((c, e) => c.Rewire(e));
    }

    public DiscBloomControl()
    {
        IsHitTestVisible = false;
        _low = Glow(0.275, 0.925, 0.65);
        _mid = Glow(0.875, 0.75, 0.5);
        _high = Glow(0.6, 0.075, 0.35);
        _vignette = Vignette();
        _onLevelsChanged = InvalidateVisual;
    }

    public WaveformPalette? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public IBrush? Base { get => GetValue(BaseProperty); set => SetValue(BaseProperty, value); }
    public IDiscBloomLevels? Levels { get => GetValue(LevelsProperty); set => SetValue(LevelsProperty, value); }

    /// <summary>The colour of the glow for a band (0 low, 1 mid, 2 high): what the palette last set.</summary>
    internal Color GlowColour(int band) => GlowFor(band).GradientStops[0].Color;

    /// <summary>The current opacity of the glow for a band (0 low, 1 mid, 2 high).</summary>
    internal double GlowOpacity(int band) => GlowFor(band).Opacity;

    private RadialGradientBrush GlowFor(int band) => band switch { 0 => _low, 1 => _mid, _ => _high };

    private RadialGradientBrush Glow(double x, double y, double radius)
    {
        var centre = new RelativePoint(x, y, RelativeUnit.Relative);
        return new RadialGradientBrush
        {
            Center = centre,
            GradientOrigin = centre,
            RadiusX = new RelativeScalar(radius, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(radius, RelativeUnit.Relative),
            Opacity = 0,
            GradientStops = { new GradientStop(default, 0.0), new GradientStop(default, 1.0) },
        };
    }

    private RadialGradientBrush Vignette()
    {
        var centre = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        return new RadialGradientBrush
        {
            Center = centre,
            GradientOrigin = centre,
            RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(default, 0.0),
                new GradientStop(default, 0.35),
                new GradientStop(default, 1.0),
            },
        };
    }

    private void Rewire(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is IDiscBloomLevels old) old.Changed -= _onLevelsChanged;
        if (e.NewValue is IDiscBloomLevels now) now.Changed += _onLevelsChanged;
        InvalidateVisual();
    }

    /// <summary>Write the palette and base colours into the cached brushes, in place.</summary>
    private void Retone()
    {
        if (Palette is { } p)
        {
            Tint(_low, p.Low);
            Tint(_mid, p.Mid);
            Tint(_high, p.High);
        }
        if (Base is ISolidColorBrush deep)
        {
            var c = deep.Color;
            _vignette.GradientStops[0].Color = Color.FromArgb(0x59, c.R, c.G, c.B);   // 35 %
            _vignette.GradientStops[1].Color = Color.FromArgb(0x1A, c.R, c.G, c.B);   // 10 %
            _vignette.GradientStops[2].Color = Color.FromArgb(0x00, c.R, c.G, c.B);
        }
    }

    // Glow stops run from the band colour at full alpha to the same colour at zero alpha, so the edge
    // fades without a dark fringe.
    private void Tint(RadialGradientBrush glow, Color c)
    {
        glow.GradientStops[0].Color = Color.FromArgb(0xFF, c.R, c.G, c.B);
        glow.GradientStops[1].Color = Color.FromArgb(0x00, c.R, c.G, c.B);
    }

    /// <summary>Copy the levels into the glow brushes' opacity (assigning an unchanged value is a no-op).</summary>
    internal void ApplyLevels(IDiscBloomLevels levels)
    {
        _low.Opacity = Math.Clamp(levels.Low, 0.0, 1.0);
        _mid.Opacity = Math.Clamp(levels.Mid, 0.0, 1.0);
        _high.Opacity = Math.Clamp(levels.High, 0.0, 1.0);
    }

    public override void Render(DrawingContext context)
    {
        if (Palette is null || Base is null || Levels is not { } levels) return;


        var rect = new Rect(Bounds.Size);
        using (context.PushClip(new RoundedRect(rect, rect.Width / 2)))
        {
            context.FillRectangle(Base, rect);
            ApplyLevels(levels);
            context.FillRectangle(_low, rect);
            context.FillRectangle(_mid, rect);
            context.FillRectangle(_high, rect);
            context.FillRectangle(_vignette, rect);
        }
    }
}

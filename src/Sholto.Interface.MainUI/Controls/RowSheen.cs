using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>
/// A soft specular spot under the pointer on a Track List row: about 150 px wide, as tall as the row, brightest
/// in the middle and falling off to nothing. It is laid over the whole row (first child, so row text and the grip
/// sit above it) and never takes the pointer. <see cref="PointerX"/> is where the spot is centred and
/// <see cref="Intensity"/> (0 to 1) how much of it shows; the view's style fades <see cref="Intensity"/> with a
/// transition, or not at all under reduced motion.
///
/// The styled-property registrations and the static constructor are forced by Avalonia. The brush is built once
/// and only its stop colours change with the theme; nothing is allocated per frame beyond structs.
/// </summary>
public sealed class RowSheen : Control
{
    /// <summary>Where the spot is centred, in px from the row's left edge.</summary>
    public static readonly StyledProperty<double> PointerXProperty =
        AvaloniaProperty.Register<RowSheen, double>(nameof(PointerX));

    /// <summary>How much of the spot shows, 0 to 1. Transitioned by the view's style.</summary>
    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<RowSheen, double>(nameof(Intensity));

    /// <summary>The spot's core colour: the theme's bright text colour.</summary>
    public static readonly StyledProperty<IBrush?> LightProperty =
        AvaloniaProperty.Register<RowSheen, IBrush?>(nameof(Light));

    /// <summary>The spot's outer colour: the Track List's violet.</summary>
    public static readonly StyledProperty<IBrush?> FringeProperty =
        AvaloniaProperty.Register<RowSheen, IBrush?>(nameof(Fringe));

    private const double SpotWidth = 150;
    private const byte CoreAlpha = 0x21;    // about 13 %
    private const byte FringeAlpha = 0x12;  // about 7 %

    private readonly RadialGradientBrush _spot;

    static RowSheen()
    {
        AffectsRender<RowSheen>(PointerXProperty, IntensityProperty, LightProperty, FringeProperty);
        LightProperty.Changed.AddClassHandler<RowSheen>((s, _) => s.Retone());
        FringeProperty.Changed.AddClassHandler<RowSheen>((s, _) => s.Retone());
    }

    public RowSheen()
    {
        IsHitTestVisible = false;
        _spot = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = RelativeScalar.Parse("50%"),
            RadiusY = RelativeScalar.Parse("50%"),
            GradientStops =
            {
                new GradientStop(default, 0.0),
                new GradientStop(default, 0.5),
                new GradientStop(default, 1.0),
            },
        };
    }

    public double PointerX { get => GetValue(PointerXProperty); set => SetValue(PointerXProperty, value); }
    public double Intensity { get => GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
    public IBrush? Light { get => GetValue(LightProperty); set => SetValue(LightProperty, value); }
    public IBrush? Fringe { get => GetValue(FringeProperty); set => SetValue(FringeProperty, value); }

    // Core light at 13 %, violet at 7 %, then nothing; the alpha is applied here so the theme brushes stay opaque.
    private void Retone()
    {
        if (Light is not ISolidColorBrush light || Fringe is not ISolidColorBrush fringe) return;
        var l = light.Color;
        var f = fringe.Color;
        _spot.GradientStops[0].Color = Color.FromArgb(CoreAlpha, l.R, l.G, l.B);
        _spot.GradientStops[1].Color = Color.FromArgb(FringeAlpha, f.R, f.G, f.B);
        _spot.GradientStops[2].Color = Color.FromArgb(0x00, f.R, f.G, f.B);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var intensity = Intensity;
        if (intensity <= 0 || Light is null || Fringe is null) return;

        using (context.PushClip(new Rect(Bounds.Size)))
        using (context.PushOpacity(Math.Min(1, intensity)))
        using (context.PushTransform(Matrix.CreateTranslation(PointerX - SpotWidth / 2, 0)))
            context.DrawRectangle(_spot, null, new Rect(0, 0, SpotWidth, Bounds.Height));
    }
}

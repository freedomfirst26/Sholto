using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>Builds the shapes the board paints over each control of the drawing: the glow
/// rings, the fill tint, and the transparent hit target. Never adds them to a canvas.</summary>
public sealed class FaceplateShapeFactory(FaceplateBrushes brushes) : IFaceplateShapeFactory
{
    /// <summary>How far the bloom reaches beyond a control's edge, as a fraction of the
    /// control's short side. A fixed width swamped a 20px button and smeared into brown
    /// haze around the 440px platter, so the rings are cut to the control they belong to
    /// and clamped at both ends.</summary>
    private const double BloomFraction = 0.16;
    private const double BloomMin = 4, BloomMax = 10;
    /// <summary>The wider, softer ring, as a multiple of the bloom. Selection only — it
    /// is what makes the held state read as stronger than hover from across the board.
    /// </summary>
    private const double HaloMultiple = 1.6;

    private readonly FaceplateBrushes _brushes = brushes;

    public FaceplatePiece CreatePiece(Control shape, Rect rect, bool implemented)
    {
        var radius = CornerRadiusOf(shape);

        var t = Math.Clamp(Math.Min(rect.Width, rect.Height) * BloomFraction, BloomMin, BloomMax);
        var halo = Ring(rect, t * HaloMultiple, t, radius, blur: t * HaloMultiple);
        var bloom = Ring(rect, t, 0, radius, blur: t * 0.8);
        var tint = Solid(rect, radius, animated: true);
        var outline = Outline(rect, radius);
        // The eye integrates a wash over its area, so the same alpha that reads as a
        // hint on a 30px button reads as paint across a 440px platter and flattens
        // the drawing under it. Big faces get proportionally less.
        var wash = WashScale(rect);
        tint.Opacity = wash;
        tint.Fill = implemented ? _brushes.AmberRest : null;

        // What the wash gives up on a big face, the rim takes back: the jog would
        // otherwise be the least-marked control on the board despite being the most
        // used one.
        return new FaceplatePiece(halo, bloom, tint, outline, implemented,
                              RestBloom: 0.20 + (1.0 - wash) * 0.40);
    }

    /// <summary>The transparent shape that answers the pointer for a control. Shared by
    /// a control's own body and by a legend that speaks for it, so the two are the same
    /// target in every respect but where they sit.</summary>
    public Shape CreateHit(Control shape, Rect rect, bool implemented)
    {
        var hit = Solid(rect, CornerRadiusOf(shape), animated: false);
        hit.Fill = Brushes.Transparent;
        hit.IsHitTestVisible = true;
        // A hand over a control Sholto never hears from contradicts the board's own
        // language: the paint says "nothing here", the pointer says "button". They
        // stay clickable — the panel explains why they do nothing, which is worth
        // reading — but the cursor no longer promises an action.
        hit.Cursor = new Cursor(implemented
        ? StandardCursorType.Hand
        : StandardCursorType.Arrow);
        return hit;
    }

    /// <summary>A blurred ring hugging the control. <paramref name="thickness"/> is how
    /// far it reaches out; <paramref name="inset"/> pushes it further out so two rings
    /// can sit concentrically without overlapping.</summary>
    private Shape Ring(Rect r, double thickness, double inset, double radius, double blur)
    {
        var outer = r.Inflate(thickness + inset);
        var s = Build(outer, radius < 0 ? radius : radius + thickness + inset);
        s.Fill = null;
        s.Stroke = _brushes.AmberGlowBrush;
        s.StrokeThickness = thickness;
        s.Effect = new BlurEffect { Radius = blur };
        s.Opacity = 0;
        Fade(s);
        Place(s, outer);
        return s;
    }

    /// <summary>The selection stroke: a 2px solid ring hugging the control, amber, no
    /// blur. Every other mark on this board is soft — washes, blurred rings, blooms —
    /// so a hard edge is a change of KIND rather than of degree, and that is the whole
    /// point of it. Selection used to be "the same glow, a bit stronger", which a
    /// 440px platter could carry and a 30px pad could not: against a field where every
    /// implemented control already blooms, one more increment of bloom is invisible.
    /// </summary>
    private Shape Outline(Rect r, double radius)
    {
        var outer = r.Inflate(3);
        var s = Build(outer, radius < 0 ? radius : radius + 3);
        s.Fill = null;
        s.Stroke = _brushes.AmberGlowBrush;
        s.StrokeThickness = 2;
        s.Opacity = 0;
        Fade(s);
        Place(s, outer);
        return s;
    }

    /// <summary>How much of the resting wash a control of this size should carry: full
    /// for anything button-sized, tapering to just over half for the jog platter.</summary>
    private double WashScale(Rect r)
    {
        var side = Math.Min(r.Width, r.Height);
        if (side <= 70) return 1.0;
        return Math.Max(0.70, 1.0 - (side - 70) / 1200.0);
    }

    /// <summary>A shape filling the control exactly. <paramref name="animated"/> is false
    /// for the hit target, whose opacity never changes.</summary>
    private Shape Solid(Rect r, double radius, bool animated)
    {
        var s = Build(r, radius);
        if (animated) Fade(s);
        Place(s, r);
        return s;
    }

    /// <summary>A negative radius means "circle" — see <see cref="CornerRadiusOf"/>. A
    /// square-cornered Border reports 0 and still wants a Rectangle, which is why the
    /// caller passes the radius rather than this guessing from the bounds.</summary>
    private Shape Build(Rect r, double radius) => radius < 0
        ? new Ellipse { Width = r.Width, Height = r.Height }
        : new Rectangle { Width = r.Width, Height = r.Height, RadiusX = radius, RadiusY = radius };

    private void Place(Control c, Rect r)
    {
        Canvas.SetLeft(c, r.X);
        Canvas.SetTop(c, r.Y);
        c.IsHitTestVisible = false;
    }

    private void Fade(Avalonia.Animation.Animatable c) => c.Transitions =
    [
        new Avalonia.Animation.DoubleTransition
        {
        Property = Visual.OpacityProperty,
        Duration = TimeSpan.FromMilliseconds(170),
        Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
        },
    ];

    private double CornerRadiusOf(Control c) => c switch
    {
        Ellipse => -1,                            // circle: no radius, use an Ellipse
        Border b => b.CornerRadius.TopLeft,
        _ => 0,
    };
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>Builds the circled step badge that numbers a control in a combination.</summary>
public sealed class FaceplateStepBadgeFactory(FaceplateBrushes brushes) : IFaceplateStepBadgeFactory
{
    private readonly FaceplateBrushes _brushes = brushes;

    public Control Create(Shape tint, int step)
    {
        var left = Canvas.GetLeft(tint);
        var top = Canvas.GetTop(tint);
        // Top-CENTRE of the shape's bounding box, not its corner: the jog is a 440px
        // circle, and a badge planted at the box corner lands off in empty space,
        // nowhere near the ring it is meant to number. Top-centre sits just above the
        // shape for every size, round or square.
        var centerX = left + tint.Width / 2;
        // U+2460 ① … circled digits read as ordered steps without competing with the
        // amber glow underneath.
        var glyph = step is >= 1 and <= 20 ? char.ConvertFromUtf32(0x2460 + step - 1) : step.ToString();
        var label = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = _brushes.AmberGlowBrush,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = glyph,
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.Black,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            },
        };
        Canvas.SetLeft(label, centerX - 11);
        Canvas.SetTop(label, top - 11);
        return label;
    }
}

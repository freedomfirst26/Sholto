using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Sholto.Interface.Faceplate.Views;

namespace Sholto.Interface.Faceplate.Tests;

public sealed class FaceplateShapeFactoryTests
{
    private readonly FaceplateShapeFactory _factory = new(new FaceplateBrushes());

    [Fact]
    public void CreatePiece_on_a_30px_rounded_border_builds_rectangles_sized_by_the_bloom()
    {
        var piece = _factory.CreatePiece(new Border { CornerRadius = new CornerRadius(6) },
                                         new Rect(100, 50, 30, 30), implemented: true);

        var halo = Assert.IsType<Rectangle>(piece.Halo);
        var bloom = Assert.IsType<Rectangle>(piece.Bloom);
        var tint = Assert.IsType<Rectangle>(piece.Tint);
        var outline = Assert.IsType<Rectangle>(piece.Outline);
        Assert.Equal(30 + 2 * (4.8 * 1.6 + 4.8), halo.Width, 1e-9);
        Assert.Equal(4.8 * 1.6, halo.StrokeThickness, 1e-9);
        Assert.Equal(30 + 2 * 4.8, bloom.Width, 1e-9);
        Assert.Equal(6 + 4.8, bloom.RadiusX, 1e-9);
        Assert.Equal(36, outline.Width, 1e-9);
        Assert.Equal(2, outline.StrokeThickness, 1e-9);
        Assert.Equal(9, outline.RadiusX, 1e-9);
        Assert.Equal(30, tint.Width, 1e-9);
        Assert.Equal(100, Canvas.GetLeft(tint), 1e-9);
        Assert.Equal(50, Canvas.GetTop(tint), 1e-9);
        Assert.Equal(1.0, tint.Opacity, 1e-9);
        Assert.Equal(0.20, piece.RestBloom, 1e-9);
        foreach (var shape in new[] { piece.Halo, piece.Bloom, piece.Outline })
            Assert.Equal(0, shape.Opacity);
        foreach (var shape in new[] { piece.Halo, piece.Bloom, piece.Tint, piece.Outline })
        {
            Assert.False(shape.IsHitTestVisible);
            Assert.Null(shape.Parent);
        }
    }

    [Fact]
    public void CreatePiece_on_a_440px_platter_scales_the_wash_down_and_the_rest_bloom_up()
    {
        var piece = _factory.CreatePiece(new Ellipse(), new Rect(0, 0, 440, 440), implemented: true);

        Assert.IsType<Ellipse>(piece.Halo);
        Assert.IsType<Ellipse>(piece.Bloom);
        Assert.IsType<Ellipse>(piece.Tint);
        Assert.IsType<Ellipse>(piece.Outline);
        Assert.Equal(0.70, piece.Tint.Opacity, 1e-9);
        Assert.Equal(0.32, piece.RestBloom, 1e-9);
        Assert.Equal(460, piece.Bloom.Width, 1e-9);
    }

    [Fact]
    public void CreatePiece_on_a_square_border_keeps_a_rectangle()
    {
        var piece = _factory.CreatePiece(new Border(), new Rect(0, 0, 30, 30), implemented: true);

        var tint = Assert.IsType<Rectangle>(piece.Tint);
        Assert.Equal(0, tint.RadiusX);
    }

    [Fact]
    public void CreatePiece_for_an_unimplemented_control_leaves_the_tint_unfilled()
    {
        var piece = _factory.CreatePiece(new Border(), new Rect(0, 0, 30, 30), implemented: false);

        Assert.Null(piece.Tint.Fill);
        Assert.False(piece.Implemented);
    }
}

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Sholto.Interface.Faceplate.Views;

namespace Sholto.Interface.Faceplate.Tests;

public sealed class FaceplateStepBadgeFactoryTests
{
    private readonly FaceplateStepBadgeFactory _badges = new(new FaceplateBrushes());

    private static Rectangle Tint()
    {
        var tint = new Rectangle { Width = 30, Height = 30 };
        Canvas.SetLeft(tint, 100);
        Canvas.SetTop(tint, 50);
        return tint;
    }

    [Fact]
    public void Create_puts_a_22px_badge_at_the_tints_top_centre()
    {
        var badge = Assert.IsType<Border>(_badges.Create(Tint(), 2));

        Assert.Equal(22, badge.Width);
        Assert.Equal(104, Canvas.GetLeft(badge));
        Assert.Equal(39, Canvas.GetTop(badge));
        Assert.False(badge.IsHitTestVisible);
        Assert.Null(badge.Parent);
        Assert.Equal("②", Assert.IsType<TextBlock>(badge.Child).Text);
    }

    [Fact]
    public void Create_writes_steps_past_twenty_as_plain_digits()
    {
        var badge = Assert.IsType<Border>(_badges.Create(Tint(), 25));

        Assert.Equal("25", Assert.IsType<TextBlock>(badge.Child).Text);
    }
}

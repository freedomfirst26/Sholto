using Avalonia.Media;
using Sholto.Interface.Faceplate.Views;

namespace Sholto.Interface.Faceplate.Tests;

/// <summary>Fixed, distinct brushes with no Visual attached.</summary>
public sealed class FakeFaceplateBrushes : IFaceplateBrushes
{
    public Color AmberGlow { get; } = Color.FromRgb(1, 2, 3);
    public IBrush AmberRest { get; } = new SolidColorBrush(Color.FromRgb(4, 5, 6));
    public IBrush AmberHoverFill { get; } = new SolidColorBrush(Color.FromRgb(7, 8, 9));
    public IBrush AmberSelectedFill { get; } = new SolidColorBrush(Color.FromRgb(10, 11, 12));
    public IBrush AmberGlowBrush => new SolidColorBrush(AmberGlow);
}

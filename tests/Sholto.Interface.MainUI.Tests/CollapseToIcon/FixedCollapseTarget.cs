using Avalonia;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.Tests.CollapseToIcon;

/// <summary>A collapse target at a point a test chooses.</summary>
internal sealed class FixedCollapseTarget(Point centre) : ICollapseTarget
{
    private readonly Point _centre = centre;

    public Point? CenterIn(Visual relativeTo) => _centre;
}

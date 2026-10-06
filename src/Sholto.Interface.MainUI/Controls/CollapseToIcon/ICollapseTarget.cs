using Avalonia;
using Avalonia.VisualTree;

namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>Anything an <see cref="IconDockedContainer"/> can collapse into: it knows where its centre is.</summary>
public interface ICollapseTarget
{
    /// <summary>The target's centre in <paramref name="relativeTo"/>'s coordinates, or null when it is not
    /// in the same visual tree yet.</summary>
    Point? CenterIn(Visual relativeTo);
}

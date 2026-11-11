using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Sholto.Interface.Faceplate.Views;

public interface IFaceplateStepBadgeFactory
{
    /// <summary>A circled step number positioned above the top centre of <paramref name="tint"/>. Not added to a canvas.</summary>
    Control Create(Shape tint, int step);
}
